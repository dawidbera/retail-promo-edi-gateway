using System;

namespace RetailEdiGateway.Core.Entities
{
 /// <summary>
 /// Represents an electronic data interchange (EDI) transaction log for outbound/inbound payloads.
 /// </summary>
 public class EdiTransaction
 {
 /// <summary>
 /// Unique identifier for the EDI transaction log.
 /// </summary>
 public Guid Id { get; set; } = Guid.NewGuid();

 /// <summary>
 /// Optional reference to the associated purchase order.
 /// </summary>
 public Guid? PurchaseOrderId { get; set; }

 /// <summary>
 /// Navigation property to the associated purchase order.
 /// </summary>
 public PurchaseOrder? PurchaseOrder { get; set; }

 /// <summary>
 /// Type of message exchanged: "ORDERS", "ORDRSP", or "DESADV".
 /// </summary>
 public string MessageType { get; set; } = string.Empty;

 /// <summary>
 /// Transaction direction: "INBOUND" or "OUTBOUND".
 /// </summary>
 public string Direction { get; set; } = string.Empty;

 /// <summary>
 /// Raw message payload content (EDIFACT text or XML).
 /// </summary>
 public string Payload { get; set; } = string.Empty;

 /// <summary>
 /// Transaction processing status (e.g., "PENDING", "SUCCESS", "FAILED").
 /// </summary>
 public string Status { get; set; } = "PENDING";

 /// <summary>
 /// Timestamp when the message was processed.
 /// </summary>
 public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

 /// <summary>
 /// The number of transmission attempts made for outbound transactions.
 /// </summary>
 public int RetryCount { get; set; }

 /// <summary>
 /// Optional error message stored if processing or transmission failed.
 /// </summary>
 public string? ErrorMessage { get; set; }
 }
}
