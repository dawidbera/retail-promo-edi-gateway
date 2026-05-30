using System;

namespace RetailEdiGateway.Core.Entities
{
 /// <summary>
 /// Represents the status of a reserved delivery time slot.
 /// </summary>
    public enum WarehouseSlotStatus
    {
        /// <summary>
        /// The slot is booked and awaiting the arrival of the shipment.
        /// </summary>
        Booked,

        /// <summary>
        /// The shipment has arrived at the distribution center.
        /// </summary>
        Arrived,

        /// <summary>
        /// The unloading process is completed.
        /// </summary>
        Completed,

        /// <summary>
        /// The booking has been cancelled.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Represents a reserved delivery time slot at a specific Distribution Center (DC) for a purchase order line.
    /// </summary>
    public class WarehouseSlot
    {
        /// <summary>
        /// Unique identifier for the warehouse slot booking.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Reference to the purchase order line associated with this slot.
        /// </summary>
        public Guid PurchaseOrderLineId { get; set; }

        /// <summary>
        /// Navigation property to the purchase order line associated with this slot.
        /// </summary>
        public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;

        /// <summary>
        /// Code identifying the target Distribution Center (e.g., "DC-WAW1", "DC-POZ2").
        /// </summary>
        public string DcCode { get; set; } = string.Empty;

        /// <summary>
        /// The reserved time for truck arrival.
        /// </summary>
        public DateTime BookedTime { get; set; }

        /// <summary>
        /// The specific unloading bay number assigned to the truck.
        /// </summary>
        public string BayNumber { get; set; } = string.Empty;

        /// <summary>
        /// Status of the booking slot.
        /// </summary>
        public WarehouseSlotStatus Status { get; set; } = WarehouseSlotStatus.Booked;

        /// <summary>
        /// Gets or sets a value indicating whether this slot has been successfully transmitted to the external WMS system.
        /// </summary>
        public bool IsSyncedToWms { get; set; }
    }
}
