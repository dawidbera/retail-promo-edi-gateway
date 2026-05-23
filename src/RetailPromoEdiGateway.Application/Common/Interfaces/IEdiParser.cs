using System;
using System.Collections.Generic;

namespace RetailPromoEdiGateway.Application.Common.Interfaces
{
    /// <summary>
    /// Parser service for incoming supplier EDI transactions.
    /// </summary>
    public interface IEdiParser
    {
        /// <summary>
        /// Parses an ORDRSP (Order Response) text payload and extracts confirmed quantities and dates.
        /// </summary>
        EdiResponseParseResult ParseOrdrsp(string payload);

        /// <summary>
        /// Parses a DESADV (Despatch Advice) text payload and extracts shipped quantities and tracking references.
        /// </summary>
        EdiDesadvParseResult ParseDesadv(string payload);
    }

    /// <summary>
    /// Represents the parsing result of an ORDRSP (Order Response) EDI message.
    /// </summary>
    public class EdiResponseParseResult
    {
        /// <summary>
        /// Gets or sets the ERP purchase order number extracted from the EDI payload.
        /// </summary>
        public string ErpOrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the supplier code identified in the EDI payload.
        /// </summary>
        public string SupplierCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of confirmed order line items parsed from the EDI message.
        /// </summary>
        public List<EdiResponseLine> Lines { get; set; } = new();
    }

    /// <summary>
    /// Represents an individual confirmed line item in an ORDRSP message.
    /// </summary>
    public class EdiResponseLine
    {
        /// <summary>
        /// Gets or sets the product identifier (EAN/SKU) for this confirmed line.
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity confirmed by the supplier.
        /// </summary>
        public int ConfirmedQty { get; set; }

        /// <summary>
        /// Gets or sets the confirmed delivery date promised by the supplier.
        /// </summary>
        public DateTime ConfirmedDate { get; set; }
    }

    /// <summary>
    /// Represents the parsing result of a DESADV (Despatch Advice) EDI message.
    /// </summary>
    public class EdiDesadvParseResult
    {
        /// <summary>
        /// Gets or sets the associated ERP purchase order number.
        /// </summary>
        public string ErpOrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the supplier code identified in the DESADV message.
        /// </summary>
        public string SupplierCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique shipping shipment identifier.
        /// </summary>
        public string ShipmentId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the logistics carrier transporting the goods.
        /// </summary>
        public string CarrierName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Serial Shipping Container Code (SSCC) for tracking.
        /// </summary>
        public string SSCC { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of shipped line items parsed from the DESADV message.
        /// </summary>
        public List<EdiDesadvLine> Lines { get; set; } = new();
    }

    /// <summary>
    /// Represents an individual shipped line item in a DESADV message.
    /// </summary>
    public class EdiDesadvLine
    {
        /// <summary>
        /// Gets or sets the product identifier (EAN/SKU) for this shipped line.
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity of product shipped by the supplier.
        /// </summary>
        public int ShippedQty { get; set; }
    }
}
