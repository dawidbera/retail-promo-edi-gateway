using System;

namespace RetailPromoEdiGateway.Core.Entities
{
    /// <summary>
    /// Represents the status of a reserved delivery time slot.
    /// </summary>
    public enum WarehouseSlotStatus
    {
        Booked,
        Arrived,
        Completed,
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
    }
}
