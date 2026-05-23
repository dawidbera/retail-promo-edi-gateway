using RetailPromoEdiGateway.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RetailPromoEdiGateway.Infrastructure.Services
{
    /// <summary>
    /// Service to parse EDIFACT messages (ORDRSP and DESADV).
    /// </summary>
    public class EdiParser : IEdiParser
    {
        /// <inheritdoc />
        public EdiResponseParseResult ParseOrdrsp(string payload)
        {
            var result = new EdiResponseParseResult();
            var segments = SplitSegments(payload);

            string? currentProductCode = null;
            int? currentQty = null;

            foreach (var segment in segments)
            {
                var parts = segment.Split('+');
                if (parts.Length == 0) continue;

                var segmentId = parts[0].Trim();

                switch (segmentId)
                {
                    case "BGM":
                        // Format: BGM+231+PO-2026-001+9
                        if (parts.Length > 2)
                        {
                            result.ErpOrderNumber = CleanEdiElement(parts[2]);
                        }
                        break;

                    case "NAD":
                        // Format: NAD+SU+SUPP-ITA-01
                        if (parts.Length > 2 && parts[1] == "SU")
                        {
                            result.SupplierCode = CleanEdiElement(parts[2]);
                        }
                        break;

                    case "LIN":
                        // Flush previous line if complete before starting a new one
                        FlushCurrentLine(result.Lines, ref currentProductCode, ref currentQty, DateTime.UtcNow);

                        // Format: LIN+1++8001234567890:EN
                        if (parts.Length > 3)
                        {
                            var subParts = parts[3].Split(':');
                            if (subParts.Length > 0)
                            {
                                currentProductCode = CleanEdiElement(subParts[0]);
                            }
                        }
                        break;

                    case "QTY":
                        // Format: QTY+21:9500:PCE
                        // 21 is status "Ordered quantity" or "Confirmed quantity" in some profiles.
                        if (parts.Length > 1)
                        {
                            var subParts = parts[1].Split(':');
                            if (subParts.Length > 1 && (subParts[0] == "21" || subParts[0] == "12"))
                            {
                                if (int.TryParse(subParts[1], out int qty))
                                {
                                    currentQty = qty;
                                }
                            }
                        }
                        break;

                    case "DTM":
                        // Format: DTM+2:20260528:102
                        // 2 is Delivery date/time, requested/promised
                        if (parts.Length > 1)
                        {
                            var subParts = parts[1].Split(':');
                            if (subParts.Length > 1 && subParts[0] == "2")
                            {
                                string dateStr = subParts[1];
                                if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsedDate))
                                {
                                    FlushCurrentLine(result.Lines, ref currentProductCode, ref currentQty, parsedDate);
                                }
                            }
                        }
                        break;
                }
            }

            // Flush the final line in case DTM didn't trigger it
            FlushCurrentLine(result.Lines, ref currentProductCode, ref currentQty, DateTime.UtcNow);

            // Fallback: If parsing failed to get PO or supplier due to variations, try simple Regex
            ApplyRegexFallbackOrdrsp(payload, result);

            return result;
        }

        /// <inheritdoc />
        public EdiDesadvParseResult ParseDesadv(string payload)
        {
            var result = new EdiDesadvParseResult();
            var segments = SplitSegments(payload);

            string? currentProductCode = null;

            foreach (var segment in segments)
            {
                var parts = segment.Split('+');
                if (parts.Length == 0) continue;

                var segmentId = parts[0].Trim();

                switch (segmentId)
                {
                    case "BGM":
                        // Format: BGM+351+SHIP-001+9
                        if (parts.Length > 2)
                        {
                            result.ShipmentId = CleanEdiElement(parts[2]);
                        }
                        break;

                    case "NAD":
                        // Format: NAD+SU+SUPP-ITA-01 or NAD+CA+DHL
                        if (parts.Length > 2)
                        {
                            if (parts[1] == "SU")
                            {
                                result.SupplierCode = CleanEdiElement(parts[2]);
                            }
                            else if (parts[1] == "CA")
                            {
                                result.CarrierName = CleanEdiElement(parts[2]);
                            }
                        }
                        break;

                    case "RFF":
                        // Format: RFF+ON:PO-2026-001
                        if (parts.Length > 1)
                        {
                            var subParts = parts[1].Split(':');
                            if (subParts.Length > 1 && subParts[0] == "ON")
                            {
                                result.ErpOrderNumber = CleanEdiElement(subParts[1]);
                            }
                        }
                        break;

                    case "GIR":
                        // Format: GIR+3+SSCC-999991234567890123:ML
                        if (parts.Length > 2 && parts[1] == "3")
                        {
                            var subParts = parts[2].Split(':');
                            if (subParts.Length > 0)
                            {
                                result.SSCC = CleanEdiElement(subParts[0]);
                            }
                        }
                        break;

                    case "LIN":
                        // Format: LIN+1++8001234567890:EN
                        if (parts.Length > 3)
                        {
                            var subParts = parts[3].Split(':');
                            if (subParts.Length > 0)
                            {
                                currentProductCode = CleanEdiElement(subParts[0]);
                            }
                        }
                        break;

                    case "QTY":
                        // Format: QTY+12:9500:PCE
                        // 12 is Shipped quantity
                        if (parts.Length > 1 && !string.IsNullOrEmpty(currentProductCode))
                        {
                            var subParts = parts[1].Split(':');
                            if (subParts.Length > 1 && subParts[0] == "12")
                            {
                                if (int.TryParse(subParts[1], out int qty))
                                {
                                    result.Lines.Add(new EdiDesadvLine
                                    {
                                        ProductCode = currentProductCode,
                                        ShippedQty = qty
                                    });
                                    currentProductCode = null; // reset
                                }
                            }
                        }
                        break;
                }
            }

            // Fallback for DESADV
            ApplyRegexFallbackDesadv(payload, result);

            return result;
        }

        /// <summary>
        /// Splits the raw EDIFACT payload into segments based on the segment terminator character (single quote).
        /// </summary>
        /// <param name="payload">The raw EDIFACT message text.</param>
        /// <returns>An array of individual EDIFACT segment strings.</returns>
        private static string[] SplitSegments(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return Array.Empty<string>();
            return payload.Split(new[] { '\'' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Cleanses individual EDIFACT elements of unnecessary quotes or whitespace padding.
        /// </summary>
        /// <param name="value">The raw element string.</param>
        /// <returns>A cleaned and trimmed element string.</returns>
        private static string CleanEdiElement(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("'", "").Trim();
        }

        /// <summary>
        /// Helper to flush a fully-built parsed line item into the resulting confirmed lines collection and resets tracking pointers.
        /// </summary>
        /// <param name="lines">The collection to accumulate the confirmed line.</param>
        /// <param name="productCode">A reference to the current product identifier being tracked.</param>
        /// <param name="qty">A reference to the confirmed quantity being tracked.</param>
        /// <param name="confirmedDate">The confirmed promised delivery date.</param>
        private static void FlushCurrentLine(List<EdiResponseLine> lines, ref string? productCode, ref int? qty, DateTime confirmedDate)
        {
            if (!string.IsNullOrEmpty(productCode) && qty.HasValue)
            {
                // Avoid duplicates
                var code = productCode;
                if (!lines.Any(l => l.ProductCode == code))
                {
                    lines.Add(new EdiResponseLine
                    {
                        ProductCode = code,
                        ConfirmedQty = qty.Value,
                        ConfirmedDate = confirmedDate
                    });
                }
                productCode = null;
                qty = null;
            }
        }

        /// <summary>
        /// Applies robust regular expression scanning as a safety fallback to extract purchase order, supplier code, and confirmed lines when standard segment splitting encounters variations.
        /// </summary>
        /// <param name="payload">The raw EDI payload text.</param>
        /// <param name="result">The accumulated parse result to populate.</param>
        private static void ApplyRegexFallbackOrdrsp(string payload, EdiResponseParseResult result)
        {
            // If PO is empty, search for PO-xxxx pattern
            if (string.IsNullOrEmpty(result.ErpOrderNumber))
            {
                var poMatch = Regex.Match(payload, @"PO-\d{4}-\d{3}");
                if (poMatch.Success)
                {
                    result.ErpOrderNumber = poMatch.Value;
                }
            }

            // If supplier is empty, search for SUPP-xxx pattern
            if (string.IsNullOrEmpty(result.SupplierCode))
            {
                var suppMatch = Regex.Match(payload, @"SUPP-[A-Z]{3}-\d{2}");
                if (suppMatch.Success)
                {
                    result.SupplierCode = suppMatch.Value;
                }
            }

            // Parse lines using regex if standard segments parsing yielded nothing
            if (!result.Lines.Any())
            {
                // Match LIN and following QTY / DTM
                var matches = Regex.Matches(payload, @"LIN\+\d+\+\+([0-9]+):EN.*?QTY\+21:(\d+):PCE.*?DTM\+2:(\d{8}):102", RegexOptions.Singleline);
                foreach (Match m in matches)
                {
                    if (m.Groups.Count >= 4)
                    {
                        var prod = m.Groups[1].Value;
                        var qtyStr = m.Groups[2].Value;
                        var dateStr = m.Groups[3].Value;

                        if (int.TryParse(qtyStr, out int qty) && 
                            DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime date))
                        {
                            result.Lines.Add(new EdiResponseLine
                            {
                                ProductCode = prod,
                                ConfirmedQty = qty,
                                ConfirmedDate = date
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Applies robust regular expression scanning as a safety fallback to extract PO, supplier, SSCC, and shipped lines for DESADV messages.
        /// </summary>
        /// <param name="payload">The raw EDI payload text.</param>
        /// <param name="result">The accumulated despatch advice parse result to populate.</param>
        private static void ApplyRegexFallbackDesadv(string payload, EdiDesadvParseResult result)
        {
            if (string.IsNullOrEmpty(result.ErpOrderNumber))
            {
                var poMatch = Regex.Match(payload, @"PO-\d{4}-\d{3}");
                if (poMatch.Success)
                {
                    result.ErpOrderNumber = poMatch.Value;
                }
            }

            if (string.IsNullOrEmpty(result.SupplierCode))
            {
                var suppMatch = Regex.Match(payload, @"SUPP-[A-Z]{3}-\d{2}");
                if (suppMatch.Success)
                {
                    result.SupplierCode = suppMatch.Value;
                }
            }

            if (string.IsNullOrEmpty(result.SSCC))
            {
                var ssccMatch = Regex.Match(payload, @"SSCC-\d{18}");
                if (ssccMatch.Success)
                {
                    result.SSCC = ssccMatch.Value;
                }
            }

            if (!result.Lines.Any())
            {
                var matches = Regex.Matches(payload, @"LIN\+\d+\+\+([0-9]+):EN.*?QTY\+12:(\d+):PCE", RegexOptions.Singleline);
                foreach (Match m in matches)
                {
                    if (m.Groups.Count >= 3)
                    {
                        var prod = m.Groups[1].Value;
                        var qtyStr = m.Groups[2].Value;

                        if (int.TryParse(qtyStr, out int qty))
                        {
                            result.Lines.Add(new EdiDesadvLine
                            {
                                ProductCode = prod,
                                ShippedQty = qty
                            });
                        }
                    }
                }
            }
        }
    }
}
