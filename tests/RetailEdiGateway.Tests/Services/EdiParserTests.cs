using RetailEdiGateway.Infrastructure.Services;
using System;
using Xunit;

namespace RetailEdiGateway.Tests.Services
{
 /// <summary>
 /// Unit tests for the <see cref="EdiParser"/> class.
 /// Verifies correct parsing of ORDRSP and DESADV messages under normal and fallback conditions.
 /// </summary>
 public class EdiParserTests
 {
 private readonly EdiParser _parser;

 /// <summary>
 /// Initializes a new instance of the <see cref="EdiParserTests"/> class.
 /// </summary>
 public EdiParserTests()
 {
 _parser = new EdiParser();
 }

 /// <summary>
 /// Verifies that <see cref="EdiParser.ParseOrdrsp"/> correctly parses standard ORDRSP segments.
 /// </summary>
 [Fact]
 public void ParseOrdrsp_StandardPayload_ReturnsParsedResult()
 {
 // Arrange
 var payload = "UNB+UNOA:2+SUPP-ITA-01+RETAIL+260524:1500+MSG01'\n" +
 "BGM+231+PO-2026-001+9'\n" +
 "NAD+SU+SUPP-ITA-01'\n" +
 "LIN+1++8001234567890:EN'\n" +
 "QTY+21:9500:PCE'\n" +
 "DTM+2:20260528:102'";

 // Act
 var result = _parser.ParseOrdrsp(payload);

 // Assert
 Assert.NotNull(result);
 Assert.Equal("PO-2026-001", result.ErpOrderNumber);
 Assert.Equal("SUPP-ITA-01", result.SupplierCode);
 Assert.Single(result.Lines);
 Assert.Equal("8001234567890", result.Lines[0].ProductCode);
 Assert.Equal(9500, result.Lines[0].ConfirmedQty);
 Assert.Equal(new DateTime(2026, 05, 28, 0, 0, 0, DateTimeKind.Utc), result.Lines[0].ConfirmedDate.ToUniversalTime());
 }

 /// <summary>
 /// Verifies that <see cref="EdiParser.ParseOrdrsp"/> utilizes regex fallback when standard segment parsing fails.
 /// </summary>
 [Fact]
 public void ParseOrdrsp_MalformedPayload_UsesRegexFallback()
 {
 // Arrange - malformed segments where split '+' won't work perfectly but regex matching succeeds
 var payload = "BGM***PO-2026-002***\n" +
 "NAD***SUPP-DEO-02***\n" +
 "LIN+1++4001122334455:EN~QTY+21:1200:PCE~DTM+2:20260610:102";

 // Act
 var result = _parser.ParseOrdrsp(payload);

 // Assert
 Assert.NotNull(result);
 Assert.Equal("PO-2026-002", result.ErpOrderNumber);
 Assert.Equal("SUPP-DEO-02", result.SupplierCode);
 Assert.Single(result.Lines);
 Assert.Equal("4001122334455", result.Lines[0].ProductCode);
 Assert.Equal(1200, result.Lines[0].ConfirmedQty);
 Assert.Equal(new DateTime(2026, 06, 10, 0, 0, 0, DateTimeKind.Utc), result.Lines[0].ConfirmedDate.ToUniversalTime());
 }

 /// <summary>
 /// Verifies that <see cref="EdiParser.ParseDesadv"/> correctly parses standard DESADV segments.
 /// </summary>
 [Fact]
 public void ParseDesadv_StandardPayload_ReturnsParsedResult()
 {
 // Arrange
 var payload = "UNB+UNOA:2+SUPP-ITA-01+RETAIL+260524:1515+MSG02'\n" +
 "BGM+351+SHIP-001+9'\n" +
 "NAD+SU+SUPP-ITA-01'\n" +
 "NAD+CA+DHL'\n" +
 "RFF+ON:PO-2026-001'\n" +
 "GIR+3+SSCC-999991234567890123:ML'\n" +
 "LIN+1++8001234567890:EN'\n" +
 "QTY+12:9500:PCE'";

 // Act
 var result = _parser.ParseDesadv(payload);

 // Assert
 Assert.NotNull(result);
 Assert.Equal("SHIP-001", result.ShipmentId);
 Assert.Equal("SUPP-ITA-01", result.SupplierCode);
 Assert.Equal("DHL", result.CarrierName);
 Assert.Equal("PO-2026-001", result.ErpOrderNumber);
 Assert.Equal("SSCC-999991234567890123", result.SSCC);
 Assert.Single(result.Lines);
 Assert.Equal("8001234567890", result.Lines[0].ProductCode);
 Assert.Equal(9500, result.Lines[0].ShippedQty);
 }

 /// <summary>
 /// Verifies that <see cref="EdiParser.ParseDesadv"/> correctly extracts fields via regex fallback when standard segments are missing.
 /// </summary>
 [Fact]
 public void ParseDesadv_MalformedPayload_UsesRegexFallback()
 {
 // Arrange
 var payload = "RFF+ON:PO-2026-002'\n" +
 "SUPP-DEO-02\n" +
 "SSCC-888888888888888888\n" +
 "LIN+1++4001122334455:EN~QTY+12:1450:PCE";

 // Act
 var result = _parser.ParseDesadv(payload);

 // Assert
 Assert.NotNull(result);
 Assert.Equal("PO-2026-002", result.ErpOrderNumber);
 Assert.Equal("SUPP-DEO-02", result.SupplierCode);
 Assert.Equal("SSCC-888888888888888888", result.SSCC);
 Assert.Single(result.Lines);
 Assert.Equal("4001122334455", result.Lines[0].ProductCode);
 Assert.Equal(1450, result.Lines[0].ShippedQty);
 }
 }
}
