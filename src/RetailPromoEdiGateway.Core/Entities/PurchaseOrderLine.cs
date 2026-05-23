using System;
using System.Collections.Generic;

namespace RetailPromoEdiGateway.Core.Entities
{
    /// <summary>
    /// Represents an individual item within a promotional purchase order.
    /// </summary>
    public class PurchaseOrderLine
    {
        /// <summary>
        /// Unique identifier for the purchase order line.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Reference to the parent purchase order.
        /// </summary>
        public Guid PurchaseOrderId { get; set; }

        /// <summary>
        /// Navigation property to the parent purchase order.
        /// </summary>
        public PurchaseOrder PurchaseOrder { get; set; } = null!;

        /// <summary>
        /// Unique product identifier / article number (e.g., EAN, SKU).
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Quantity ordered initially by the retail ERP.
        /// </summary>
        public int OrderedQty { get; set; }

        /// <summary>
        /// Quantity confirmed by the supplier via ORDRSP message.
        /// </summary>
        public int ConfirmedQty { get; set; }

        /// <summary>
        /// Requested delivery date as scheduled in the ERP.
        /// </summary>
        public DateTime RequestedDate { get; set; }

        /// <summary>
        /// Confirmed delivery date promised by the supplier via ORDRSP message.
        /// </summary>
        public DateTime? ConfirmedDate { get; set; }

        /// <summary>
        /// Line status representing the state of this specific purchase order line.
        /// </summary>
        public PurchaseOrderLineStatus Status { get; set; } = PurchaseOrderLineStatus.Pending;

        /// <summary>
        /// Warehouse delivery time slots scheduled for this specific order line.
        /// </summary>
        public ICollection<WarehouseSlot> WarehouseSlots { get; set; } = new List<WarehouseSlot>();
    }

    /// <summary>
    /// Represents the status of an individual item within a promotional purchase order.
    /// </summary>
    public enum PurchaseOrderLineStatus
    {
        /// <summary>
        /// The order line has been sent to the supplier but no response has been received yet.
        /// </summary>
        Pending,

        /// <summary>
        /// The supplier has confirmed quantity and delivery date matching constraints.
        /// </summary>
        Confirmed,

        /// <summary>
        /// The supplier confirmed quantities or dates that conflict with requested constraints.
        /// </summary>
        Mismatched,

        /// <summary>
        /// The supplier has dispatched the goods and sent a despatch advice (DESADV).
        /// </summary>
        Shipped,

        /// <summary>
        /// The goods have arrived and been successfully unloaded at the distribution center.
        /// </summary>
        Delivered
    }
}
