using MediatR;
using Microsoft.EntityFrameworkCore;
using RetailPromoEdiGateway.Application.Common.Interfaces;
using RetailPromoEdiGateway.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Application.Features.Campaigns.Queries
{
    /// <summary>
    /// Query to retrieve the dashboard overview of all promotional campaigns and their metrics.
    /// </summary>
    public class GetCampaignDashboardQuery : IRequest<List<CampaignDashboardDto>>
    {
    }

    /// <summary>
    /// Data Transfer Object representing the dashboard summary of a promotional campaign.
    /// </summary>
    public class CampaignDashboardDto
    {
        /// <summary>
        /// Gets or sets the unique campaign identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the campaign.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start date of the promotional campaign in stores.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Gets or sets the deadline for goods to arrive at the distribution center.
        /// </summary>
        public DateTime DeliveryDeadline { get; set; }

        /// <summary>
        /// Gets or sets the overall status of the campaign.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of items ordered across all purchase orders in this campaign.
        /// </summary>
        public int TotalItemsOrdered { get; set; }

        /// <summary>
        /// Gets or sets the total number of items confirmed by suppliers.
        /// </summary>
        public int TotalItemsConfirmed { get; set; }

        /// <summary>
        /// Gets or sets the total number of items shipped.
        /// </summary>
        public int TotalItemsShipped { get; set; }

        /// <summary>
        /// Gets the fulfillment percentage based on ordered and confirmed quantities.
        /// </summary>
        public double FulfillmentPercentage => TotalItemsOrdered == 0 ? 0 : Math.Round((double)TotalItemsConfirmed / TotalItemsOrdered * 100, 2);

        /// <summary>
        /// Gets the delivery percentage based on ordered and shipped quantities.
        /// </summary>
        public double DeliveryPercentage => TotalItemsOrdered == 0 ? 0 : Math.Round((double)TotalItemsShipped / TotalItemsOrdered * 100, 2);

        /// <summary>
        /// Gets or sets the list of purchase orders associated with this campaign.
        /// </summary>
        public List<CampaignOrderDto> Orders { get; set; } = new();
    }

    /// <summary>
    /// Data Transfer Object representing a purchase order inside a campaign dashboard.
    /// </summary>
    public class CampaignOrderDto
    {
        /// <summary>
        /// Gets or sets the unique purchase order identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the ERP-generated order number.
        /// </summary>
        public string ErpOrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the supplier.
        /// </summary>
        public string SupplierName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique supplier code.
        /// </summary>
        public string SupplierCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the processing status of the purchase order.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of detailed order lines.
        /// </summary>
        public List<CampaignOrderLineDto> Lines { get; set; } = new();
    }

    /// <summary>
    /// Data Transfer Object representing a single purchase order line item.
    /// </summary>
    public class CampaignOrderLineDto
    {
        /// <summary>
        /// Gets or sets the unique purchase order line identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique product code (EAN/SKU).
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable name of the product.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity ordered initially.
        /// </summary>
        public int OrderedQty { get; set; }

        /// <summary>
        /// Gets or sets the quantity confirmed by the supplier.
        /// </summary>
        public int ConfirmedQty { get; set; }

        /// <summary>
        /// Gets or sets the line status as a string representation of the enum.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the requested delivery date.
        /// </summary>
        public DateTime RequestedDate { get; set; }

        /// <summary>
        /// Gets or sets the confirmed delivery date promised by the supplier.
        /// </summary>
        public DateTime? ConfirmedDate { get; set; }

        /// <summary>
        /// Gets or sets the warehouse slots booked for this order line.
        /// </summary>
        public List<WarehouseSlotDto> BookedSlots { get; set; } = new();
    }

    /// <summary>
    /// Data Transfer Object representing a booked warehouse slot for delivery.
    /// </summary>
    public class WarehouseSlotDto
    {
        /// <summary>
        /// Gets or sets the unique slot identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the distribution center code.
        /// </summary>
        public string DcCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the booked arrival time.
        /// </summary>
        public DateTime BookedTime { get; set; }

        /// <summary>
        /// Gets or sets the unloading bay number assigned to the truck.
        /// </summary>
        public string BayNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the warehouse slot booking.
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handler to execute the query and project the result from EF Core.
    /// </summary>
    public class GetCampaignDashboardQueryHandler : IRequestHandler<GetCampaignDashboardQuery, List<CampaignDashboardDto>>
    {
        private readonly IApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetCampaignDashboardQueryHandler"/> class.
        /// </summary>
        /// <param name="context">The decoupled application database context.</param>
        public GetCampaignDashboardQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Handles the retrieval and projection of all promotional campaigns and their dashboard metrics.
        /// </summary>
        /// <param name="request">The dashboard query request.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A list of campaign dashboard summary DTOs.</returns>
        public async Task<List<CampaignDashboardDto>> Handle(GetCampaignDashboardQuery request, CancellationToken cancellationToken)
        {
            // Use EF Core projections for optimized performance, loading only necessary fields
            var campaigns = await _context.Campaigns
                .Include(c => c.Orders)
                    .ThenInclude(o => o.Supplier)
                .Include(c => c.Orders)
                    .ThenInclude(o => o.Lines)
                        .ThenInclude(l => l.WarehouseSlots)
                .OrderBy(c => c.StartDate)
                .ToListAsync(cancellationToken);

            var result = new List<CampaignDashboardDto>();

            foreach (var campaign in campaigns)
            {
                var totalOrdered = campaign.Orders.SelectMany(o => o.Lines).Sum(l => l.OrderedQty);
                var totalConfirmed = campaign.Orders.SelectMany(o => o.Lines).Sum(l => l.ConfirmedQty);
                var totalShipped = campaign.Orders.SelectMany(o => o.Lines)
                    .Where(l => l.Status == PurchaseOrderLineStatus.Shipped || l.Status == PurchaseOrderLineStatus.Delivered)
                    .Sum(l => l.ConfirmedQty); // Shipped confirmed qty

                var dto = new CampaignDashboardDto
                {
                    Id = campaign.Id,
                    Name = campaign.Name,
                    StartDate = campaign.StartDate,
                    DeliveryDeadline = campaign.DeliveryDeadline,
                    Status = campaign.Status,
                    TotalItemsOrdered = totalOrdered,
                    TotalItemsConfirmed = totalConfirmed,
                    TotalItemsShipped = totalShipped,
                    Orders = campaign.Orders.Select(o => new CampaignOrderDto
                    {
                        Id = o.Id,
                        ErpOrderNumber = o.ErpOrderNumber,
                        SupplierName = o.Supplier.Name,
                        SupplierCode = o.Supplier.Code,
                        Status = o.Status.ToString(),
                        Lines = o.Lines.Select(l => new CampaignOrderLineDto
                        {
                            Id = l.Id,
                            ProductCode = l.ProductCode,
                            ProductName = l.ProductName,
                            OrderedQty = l.OrderedQty,
                            ConfirmedQty = l.ConfirmedQty,
                            Status = l.Status.ToString(),
                            RequestedDate = l.RequestedDate,
                            ConfirmedDate = l.ConfirmedDate,
                            BookedSlots = l.WarehouseSlots.Select(ws => new WarehouseSlotDto
                            {
                                Id = ws.Id,
                                DcCode = ws.DcCode,
                                BookedTime = ws.BookedTime,
                                BayNumber = ws.BayNumber,
                                Status = ws.Status.ToString()
                            }).ToList()
                        }).ToList()
                    }).ToList()
                };

                result.Add(dto);
            }

            return result;
        }
    }
}
