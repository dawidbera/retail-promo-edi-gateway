using System;
using System.Collections.Generic;

namespace RetailPromoEdiGateway.Core.Entities
{
    /// <summary>
    /// Represents a high-priority temporary "In & Out" promotional campaign in a retail environment.
    /// </summary>
    public class Campaign
    {
        /// <summary>
        /// Unique identifier for the campaign.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The name of the campaign, e.g., "Tydzień Włoski 2026", "Spring Gardening Week".
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Start date of the promotional campaign in stores.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Deadline for goods to arrive at the distribution center.
        /// </summary>
        public DateTime DeliveryDeadline { get; set; }

        /// <summary>
        /// Overall status of the campaign (e.g., "Scheduled", "Active", "Completed").
        /// </summary>
        public string Status { get; set; } = "Scheduled";

        /// <summary>
        /// Navigation property to all purchase orders associated with this campaign.
        /// </summary>
        public ICollection<PurchaseOrder> Orders { get; set; } = new List<PurchaseOrder>();
    }
}
