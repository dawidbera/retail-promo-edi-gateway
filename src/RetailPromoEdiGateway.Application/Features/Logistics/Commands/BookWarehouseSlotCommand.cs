using MediatR;
using Microsoft.EntityFrameworkCore;
using RetailPromoEdiGateway.Application.Common.Interfaces;
using RetailPromoEdiGateway.Core.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Application.Features.Logistics.Commands
{
    /// <summary>
    /// Command to reserve a warehouse delivery time slot for an incoming supplier shipment.
    /// </summary>
    public class BookWarehouseSlotCommand : IRequest<WarehouseSlotResponseDto>
    {
        /// <summary>
        /// Gets or sets the unique purchase order line item ID that this booking slot is for.
        /// </summary>
        public Guid PurchaseOrderLineId { get; set; }

        /// <summary>
        /// Gets or sets the distribution center code where delivery is scheduled.
        /// </summary>
        public string DcCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the booked delivery arrival time.
        /// </summary>
        public DateTime BookedTime { get; set; }

        /// <summary>
        /// Gets or sets the specific unloading bay number assigned to this booking.
        /// </summary>
        public string BayNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data Transfer Object representing the outcome of booking a warehouse slot.
    /// </summary>
    public class WarehouseSlotResponseDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether the booking was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a descriptive message detailing the booking result or collision warning details.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique ID of the created warehouse slot booking if successful.
        /// </summary>
        public Guid? SlotId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there was an overlapping booking warning (traffic collision).
        /// </summary>
        public bool HasCollisionWarning { get; set; }

        /// <summary>
        /// Gets or sets structural details about any detected booking conflict.
        /// </summary>
        public string? CollisionDetails { get; set; }
    }

    /// <summary>
    /// Handler to register the slot booking and perform conflict checking (bottleneck prevention) across suppliers.
    /// </summary>
    public class BookWarehouseSlotCommandHandler : IRequestHandler<BookWarehouseSlotCommand, WarehouseSlotResponseDto>
    {
        private readonly IApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="BookWarehouseSlotCommandHandler"/> class.
        /// </summary>
        /// <param name="context">The decoupled application database context.</param>
        public BookWarehouseSlotCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Handles the booking request by checking for DC/Bay collisions, creating a WarehouseSlot entity, and persisting it.
        /// </summary>
        /// <param name="request">The incoming booking command details.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A DTO describing slot assignment, status, and warnings.</returns>
        public async Task<WarehouseSlotResponseDto> Handle(BookWarehouseSlotCommand request, CancellationToken cancellationToken)
        {
            // Verify order line exists
            var poLine = await _context.PurchaseOrderLines
                .Include(l => l.PurchaseOrder)
                    .ThenInclude(o => o.Supplier)
                .FirstOrDefaultAsync(l => l.Id == request.PurchaseOrderLineId, cancellationToken);

            if (poLine == null)
            {
                return new WarehouseSlotResponseDto
                {
                    Success = false,
                    Message = $"Purchase order line '{request.PurchaseOrderLineId}' not found."
                };
            }

            // Check for collision (same DC, same bay, within 30 minutes window)
            var startTime = request.BookedTime.AddMinutes(-30);
            var endTime = request.BookedTime.AddMinutes(30);

            var conflictingSlots = await _context.WarehouseSlots
                .Include(s => s.PurchaseOrderLine)
                    .ThenInclude(l => l.PurchaseOrder)
                        .ThenInclude(o => o.Supplier)
                .Where(s => s.DcCode == request.DcCode 
                            && s.BayNumber == request.BayNumber 
                            && s.BookedTime >= startTime 
                            && s.BookedTime <= endTime
                            && s.Status != WarehouseSlotStatus.Cancelled)
                .ToListAsync(cancellationToken);

            // Filter for different supplier collision
            var supplierConflict = conflictingSlots
                .FirstOrDefault(s => s.PurchaseOrderLine.PurchaseOrder.SupplierId != poLine.PurchaseOrder.SupplierId);

            bool hasWarning = supplierConflict != null;
            string? warningDetails = null;

            if (hasWarning && supplierConflict != null)
            {
                var otherSupplier = supplierConflict.PurchaseOrderLine.PurchaseOrder.Supplier.Name;
                var otherPo = supplierConflict.PurchaseOrderLine.PurchaseOrder.ErpOrderNumber;
                warningDetails = $"Conflict detected at {request.DcCode} Bay {request.BayNumber}. Supplier '{otherSupplier}' (PO: {otherPo}) is already booked at {supplierConflict.BookedTime:HH:mm}.";
            }

            // Save the booking slot
            var slot = new WarehouseSlot
            {
                PurchaseOrderLineId = request.PurchaseOrderLineId,
                DcCode = request.DcCode,
                BookedTime = request.BookedTime,
                BayNumber = request.BayNumber,
                Status = WarehouseSlotStatus.Booked
            };

            _context.WarehouseSlots.Add(slot);
            await _context.SaveChangesAsync(cancellationToken);

            return new WarehouseSlotResponseDto
            {
                Success = true,
                SlotId = slot.Id,
                HasCollisionWarning = hasWarning,
                CollisionDetails = warningDetails,
                Message = hasWarning 
                    ? $"Slot booked with WARNING: {warningDetails}"
                    : "Warehouse slot booked successfully."
            };
        }
    }
}
