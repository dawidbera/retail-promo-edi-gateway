using System;
using System.Collections.Generic;

namespace RetailEdiGateway.Core.Entities
{
 /// <summary>
 /// Represents a purchase order (PO) issued to a supplier for a specific campaign.
 /// </summary>
 public class PurchaseOrder
 {
 /// <summary>
 /// Unique identifier for the purchase order.
 /// </summary>
 public Guid Id { get; set; } = Guid.NewGuid();

 /// <summary>
 /// Reference to the associated campaign.
 /// </summary>
 public Guid CampaignId { get; set; }

 /// <summary>
 /// Navigation property to the associated campaign.
 /// </summary>
 public Campaign Campaign { get; set; } = null!;

 /// <summary>
 /// Reference to the supplier that receives this purchase order.
 /// </summary>
 public Guid SupplierId { get; set; }

 /// <summary>
 /// Navigation property to the supplier that receives this purchase order.
 /// </summary>
 public Supplier Supplier { get; set; } = null!;

 /// <summary>
 /// Unique purchase order identifier generated in the ERP system.
 /// </summary>
 public string ErpOrderNumber { get; set; } = string.Empty;

 /// <summary>
 /// Current processing status representing the lifecycle state of this purchase order.
 /// </summary>
 public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

 /// <summary>
 /// Timestamp when the order was created in the gateway database.
 /// </summary>
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

 /// <summary>
 /// Detailed order lines of items in this purchase order.
 /// </summary>
 public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();

 /// <summary>
 /// Log of inbound and outbound EDI transactions associated with this PO.
 /// </summary>
 public ICollection<EdiTransaction> EdiTransactions { get; set; } = new List<EdiTransaction>();
 }

 /// <summary>
 /// Represents the processing status of a purchase order.
 /// </summary>
 public enum PurchaseOrderStatus
 {
 /// <summary>
 /// The purchase order is being drafted and not yet dispatched to the supplier.
 /// </summary>
 Draft,

 /// <summary>
 /// The purchase order has been successfully dispatched to the supplier.
 /// </summary>
 Sent,

 /// <summary>
 /// The supplier has acknowledged and confirmed part of the order lines.
 /// </summary>
 PartiallyConfirmed,

 /// <summary>
 /// The supplier has fully confirmed all order lines matching all constraints.
 /// </summary>
 Confirmed,

 /// <summary>
 /// Discrepancies were detected during order confirmation.
 /// </summary>
 Mismatched,

 /// <summary>
 /// The goods have been dispatched from the supplier's warehouse (DESADV received).
 /// </summary>
 Shipped
 }
}
