using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailPromoEdiGateway.Application.Common.Interfaces;
using RetailPromoEdiGateway.Core.Entities;
using System;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Web.Controllers.Api
{
    /// <summary>
    /// API Controller for Logistics integration (WMS callback/status updates).
    /// </summary>
    [ApiController]
    [Route("api/v1/logistics")]
    public class LogisticsApiController : ControllerBase
    {
        private readonly IApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogisticsApiController"/> class.
        /// </summary>
        public LogisticsApiController(IApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API endpoint for WMS to push updates regarding warehouse delivery slot status and bay re-assignments.
        /// </summary>
        /// <param name="request">The slot status update payload.</param>
        [HttpPost]
        [Route("slots")]
        public async Task<IActionResult> UpdateSlotStatus([FromBody] WmsSlotUpdateRequest request)
        {
            if (request == null || request.SlotId == Guid.Empty)
            {
                return BadRequest("Invalid slot update payload.");
            }

            var slot = await _context.WarehouseSlots
                .Include(s => s.PurchaseOrderLine)
                    .ThenInclude(l => l.PurchaseOrder)
                .FirstOrDefaultAsync(s => s.Id == request.SlotId);

            if (slot == null)
            {
                return NotFound($"Warehouse slot with ID '{request.SlotId}' not found.");
            }

            // Update status (e.g., "Arrived", "Completed", "Cancelled")
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (Enum.TryParse<WarehouseSlotStatus>(request.Status, true, out var parsedStatus))
                {
                    slot.Status = parsedStatus;

                    // Business logic mapping: if slot is completed, we could potentially update order line status
                    if (parsedStatus == WarehouseSlotStatus.Completed)
                    {
                        slot.PurchaseOrderLine.Status = PurchaseOrderLineStatus.Delivered;
                    }
                }
                else
                {
                    return BadRequest($"Invalid slot status value: '{request.Status}'. Valid values are: Booked, Arrived, Completed, Cancelled.");
                }
            }

            // Optional bay re-assignment from WMS
            if (!string.IsNullOrWhiteSpace(request.BayNumber))
            {
                slot.BayNumber = request.BayNumber;
            }

            await _context.SaveChangesAsync(default);

            return Ok(new
            {
                Success = true,
                Message = $"Slot '{slot.Id}' updated successfully.",
                SlotId = slot.Id,
                Status = slot.Status,
                BayNumber = slot.BayNumber,
                LineStatus = slot.PurchaseOrderLine.Status
            });
        }
    }

    /// <summary>
    /// Payload structure representing updates to slot bookings sent by the Warehouse Management System.
    /// </summary>
    public class WmsSlotUpdateRequest
    {
        /// <summary>
        /// Unique identifier for the warehouse slot booking.
        /// </summary>
        public Guid SlotId { get; set; }

        /// <summary>
        /// Updated status for the slot (e.g. "Arrived", "Completed", "Cancelled").
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Optional updated bay number assigned to the truck.
        /// </summary>
        public string? BayNumber { get; set; }
    }
}
