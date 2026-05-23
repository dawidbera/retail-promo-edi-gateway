using System;
using System.Collections.Generic;

namespace RetailPromoEdiGateway.Core.Entities
{
    /// <summary>
    /// Represents a supplier providing items for campaigns.
    /// </summary>
    public class Supplier
    {
        /// <summary>
        /// Unique identifier for the supplier.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Unique supplier code used in ERP and EDI communications (e.g., GLN or code).
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Legal name of the supplier.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The channel type used for sending EDI documents (e.g., "EDIFACT", "XML", "JSON").
        /// </summary>
        public string IntegrationType { get; set; } = "EDIFACT";

        /// <summary>
        /// Primary contact email for alerting and dispatch failure notifications.
        /// </summary>
        public string ContactEmail { get; set; } = string.Empty;

        /// <summary>
        /// Purchase orders issued to this supplier.
        /// </summary>
        public ICollection<PurchaseOrder> Orders { get; set; } = new List<PurchaseOrder>();
    }
}
