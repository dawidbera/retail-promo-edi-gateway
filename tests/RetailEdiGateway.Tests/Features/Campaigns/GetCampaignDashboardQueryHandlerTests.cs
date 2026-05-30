using Microsoft.EntityFrameworkCore;
using RetailEdiGateway.Application.Features.Campaigns.Queries;
using RetailEdiGateway.Core.Entities;
using RetailEdiGateway.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RetailEdiGateway.Tests.Features.Campaigns
{
    /// <summary>
    /// Unit tests for the <see cref="GetCampaignDashboardQueryHandler"/> class.
    /// Verifies the aggregation of campaign metrics and DTO projection.
    /// </summary>
    public class GetCampaignDashboardQueryHandlerTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        /// <summary>
        /// Verifies that the handler correctly calculates fulfillment and delivery percentages.
        /// </summary>
        [Fact]
        public async Task Handle_WithExistingData_ReturnsCorrectCalculations()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var campaignId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            using (var context = new ApplicationDbContext(options))
            {
                var supplier = new Supplier { Id = supplierId, Code = "SUPP-01", Name = "Supplier 01" };
                var campaign = new Campaign
                {
                    Id = campaignId,
                    Name = "Test Campaign",
                    StartDate = DateTime.UtcNow.AddDays(10),
                    DeliveryDeadline = DateTime.UtcNow.AddDays(5),
                    Status = "Active"
                };

                var po = new PurchaseOrder
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId,
                    SupplierId = supplierId,
                    ErpOrderNumber = "PO-001",
                    Status = PurchaseOrderStatus.Confirmed
                };

                // Line 1: 100 ordered, 80 confirmed, 80 shipped
                po.Lines.Add(new PurchaseOrderLine
                {
                    ProductCode = "P1",
                    ProductName = "Prod 1",
                    OrderedQty = 100,
                    ConfirmedQty = 80,
                    Status = PurchaseOrderLineStatus.Shipped
                });

                // Line 2: 50 ordered, 50 confirmed, 0 shipped
                po.Lines.Add(new PurchaseOrderLine
                {
                    ProductCode = "P2",
                    ProductName = "Prod 2",
                    OrderedQty = 50,
                    ConfirmedQty = 50,
                    Status = PurchaseOrderLineStatus.Confirmed
                });

                context.Suppliers.Add(supplier);
                context.Campaigns.Add(campaign);
                context.PurchaseOrders.Add(po);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var handler = new GetCampaignDashboardQueryHandler(context);
                var query = new GetCampaignDashboardQuery();

                // Act
                var result = await handler.Handle(query, CancellationToken.None);

                // Assert
                Assert.Single(result);
                var dashboard = result[0];
                Assert.Equal(150, dashboard.TotalItemsOrdered);   // 100 + 50
                Assert.Equal(130, dashboard.TotalItemsConfirmed); // 80 + 50
                Assert.Equal(80, dashboard.TotalItemsShipped);    // 80

                // 130 / 150 * 100 = 86.67
                Assert.Equal(86.67, dashboard.FulfillmentPercentage);
                // 80 / 150 * 100 = 53.33
                Assert.Equal(53.33, dashboard.DeliveryPercentage);
            }
        }

        /// <summary>
        /// Verifies that the handler returns an empty list when no campaigns exist.
        /// </summary>
        [Fact]
        public async Task Handle_NoCampaigns_ReturnsEmptyList()
        {
            // Arrange
            using var context = new ApplicationDbContext(CreateNewContextOptions());
            var handler = new GetCampaignDashboardQueryHandler(context);
            var query = new GetCampaignDashboardQuery();

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Empty(result);
        }
    }
}
