using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailPromoEdiGateway.Application.Common.Interfaces;
using RetailPromoEdiGateway.Application.Features.Logistics.Commands;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Web.Controllers
{
    /// <summary>
    /// MVC Controller to coordinate logistics and warehouse slot booking.
    /// </summary>
    public class LogisticsController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogisticsController"/> class.
        /// </summary>
        /// <param name="mediator">The CQRS mediator service.</param>
        /// <param name="context">The decoupled application database context.</param>
        public LogisticsController(IMediator mediator, IApplicationDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        /// <summary>
        /// Displays the warehouse scheduling grid, current bookings, and order lines awaiting slot allocation.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Load all bookings
            var slots = await _context.WarehouseSlots
                .Include(s => s.PurchaseOrderLine)
                    .ThenInclude(l => l.PurchaseOrder)
                        .ThenInclude(o => o.Supplier)
                .OrderBy(s => s.BookedTime)
                .ToListAsync();

            // Load all pending order lines that do not have a warehouse slot booked yet
            var pendingLines = await _context.PurchaseOrderLines
                .Include(l => l.PurchaseOrder)
                    .ThenInclude(o => o.Supplier)
                .Where(l => !l.WarehouseSlots.Any())
                .ToListAsync();

            ViewBag.Slots = slots;
            ViewBag.PendingLines = pendingLines;

            return View();
        }

        /// <summary>
        /// POST handler to book a new warehouse slot.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Book(Guid purchaseOrderLineId, string dcCode, DateTime bookedTime, string bayNumber)
        {
            if (string.IsNullOrEmpty(dcCode) || string.IsNullOrEmpty(bayNumber) || bookedTime == default)
            {
                TempData["ErrorMessage"] = "Invalid booking details provided.";
                return RedirectToAction(nameof(Index));
            }

            var command = new BookWarehouseSlotCommand
            {
                PurchaseOrderLineId = purchaseOrderLineId,
                DcCode = dcCode,
                BookedTime = DateTime.SpecifyKind(bookedTime, DateTimeKind.Utc),
                BayNumber = bayNumber
            };

            var response = await _mediator.Send(command);

            if (!response.Success)
            {
                TempData["ErrorMessage"] = response.Message;
            }
            else if (response.HasCollisionWarning)
            {
                TempData["WarningMessage"] = response.Message;
            }
            else
            {
                TempData["SuccessMessage"] = response.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
