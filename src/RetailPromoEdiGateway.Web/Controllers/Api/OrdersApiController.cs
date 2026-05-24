using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailPromoEdiGateway.Application.Common.Interfaces;
using RetailPromoEdiGateway.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Web.Controllers.Api
{
    /// <summary>
    /// API Controller for receiving Purchase Orders pushed from the ERP system.
    /// </summary>
    [ApiController]
    [Route("api/v1/orders")]
    public class OrdersApiController : ControllerBase
    {
        private readonly IApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdersApiController"/> class.
        /// </summary>
        /// <param name="context">The decoupled application database context.</param>
        public OrdersApiController(IApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API endpoint for ERP to push Purchase Orders into the EDI Gateway.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ErpOrderNumber))
            {
                return BadRequest("Invalid order request payload.");
            }

            // Check if order already exists
            var existing = await _context.PurchaseOrders
                .AnyAsync(o => o.ErpOrderNumber == request.ErpOrderNumber);
            if (existing)
            {
                return Conflict($"Order with ERP number '{request.ErpOrderNumber}' already exists.");
            }

            // Verify campaign and supplier exist
            var campaign = await _context.Campaigns.FindAsync(request.CampaignId);
            if (campaign == null)
            {
                return NotFound($"Campaign '{request.CampaignId}' not found.");
            }

            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Code == request.SupplierCode);
            if (supplier == null)
            {
                return NotFound($"Supplier '{request.SupplierCode}' not found.");
            }

            // Map and save order
            var order = new PurchaseOrder
            {
                CampaignId = request.CampaignId,
                SupplierId = supplier.Id,
                ErpOrderNumber = request.ErpOrderNumber,
                Status = PurchaseOrderStatus.Sent,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in request.Lines)
            {
                order.Lines.Add(new PurchaseOrderLine
                {
                    ProductCode = item.ProductCode,
                    ProductName = item.ProductName,
                    OrderedQty = item.OrderedQty,
                    ConfirmedQty = 0,
                    RequestedDate = item.RequestedDate,
                    Status = PurchaseOrderLineStatus.Pending
                });
            }

            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync(default);

            // Log Outbound EDI (Simulation: ORDERS document dispatched to supplier queue)
            var ediTransaction = new EdiTransaction
            {
                PurchaseOrderId = order.Id,
                MessageType = "ORDERS",
                Direction = "OUTBOUND",
                Payload = $"UNB+UNOA:2+RETAIL+{supplier.Code}+260524:1420+MSG01'\nBGM+220+{order.ErpOrderNumber}+9'\nNAD+SU+{supplier.Code}'" + 
                          string.Join("", order.Lines.Select((l, idx) => $"\nLIN+{idx + 1}++{l.ProductCode}:EN'\nQTY+21:{l.OrderedQty}:PCE'\nDTM+2:{l.RequestedDate:yyyyMMdd}:102'")),
                Status = "PENDING",
                ProcessedAt = DateTime.UtcNow
            };

            _context.EdiTransactions.Add(ediTransaction);
            await _context.SaveChangesAsync(default);

            return CreatedAtAction(null, new { id = order.Id, status = order.Status });
        }
    }

    /// <summary>
    /// Request payload representing a new purchase order to be registered in the gateway.
    /// </summary>
    public class CreateOrderRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the associated promotional campaign.
        /// </summary>
        public Guid CampaignId { get; set; }

        /// <summary>
        /// Gets or sets the code identifying the receiving supplier.
        /// </summary>
        public string SupplierCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ERP-generated order number.
        /// </summary>
        public string ErpOrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of ordered line items.
        /// </summary>
        public List<CreateOrderLineRequest> Lines { get; set; } = new();
    }

    /// <summary>
    /// Request payload representing an individual line item inside a new purchase order.
    /// </summary>
    public class CreateOrderLineRequest
    {
        /// <summary>
        /// Gets or sets the unique product identifier code (EAN/SKU).
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity ordered.
        /// </summary>
        public int OrderedQty { get; set; }

        /// <summary>
        /// Gets or sets the requested delivery arrival date.
        /// </summary>
        public DateTime RequestedDate { get; set; }
    }
}
