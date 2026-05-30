using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailEdiGateway.Core.Entities;
using RetailEdiGateway.Infrastructure.Persistence;
using RetailEdiGateway.Web.Controllers.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RetailEdiGateway.Tests.Controllers.Api
{
    /// <summary>
    /// Integration/Unit tests for the <see cref="OrdersApiController"/>.
    /// Verifies the creation of purchase orders and associated EDI transactions via the API endpoint.
    /// </summary>
    public class OrdersApiControllerTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        /// <summary>
        /// Verifies that <see cref="OrdersApiController.CreateOrder"/> successfully creates a PO and logs an outbound EDI transaction.
        /// </summary>
        [Fact]
        public async Task CreateOrder_ValidRequest_CreatesOrderAndEdiTransaction()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var campaignId = Guid.NewGuid();
            var supplierCode = "SUPP-TEST";

            using (var context = new ApplicationDbContext(options))
            {
                context.Campaigns.Add(new Campaign { Id = campaignId, Name = "Test Campaign" });
                context.Suppliers.Add(new Supplier { Id = Guid.NewGuid(), Code = supplierCode, Name = "Test Supplier" });
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var controller = new OrdersApiController(context);
                var request = new CreateOrderRequest
                {
                    CampaignId = campaignId,
                    SupplierCode = supplierCode,
                    ErpOrderNumber = "ERP-PO-001",
                    Lines = new List<CreateOrderLineRequest>
                    {
                        new() { ProductCode = "P1", ProductName = "Prod 1", OrderedQty = 100, RequestedDate = DateTime.UtcNow.AddDays(7) }
                    }
                };

                // Act
                var result = await controller.CreateOrder(request);

                // Assert
                var createdResult = Assert.IsType<CreatedAtActionResult>(result);
                Assert.NotNull(createdResult.Value);

                // Verify DB state
                var po = await context.PurchaseOrders
                    .Include(o => o.Lines)
                    .Include(o => o.EdiTransactions)
                    .FirstOrDefaultAsync(o => o.ErpOrderNumber == "ERP-PO-001");

                Assert.NotNull(po);
                Assert.Equal(PurchaseOrderStatus.Sent, po.Status);
                Assert.Single(po.Lines);
                Assert.Single(po.EdiTransactions);

                var edi = po.EdiTransactions.First();
                Assert.Equal(EdiMessageType.Orders, edi.MessageType);
                Assert.Equal(EdiDirection.Outbound, edi.Direction);
                Assert.Equal(EdiTransactionStatus.Pending, edi.Status);
                Assert.Contains("ERP-PO-001", edi.Payload);
            }
        }

        /// <summary>
        /// Verifies that <see cref="OrdersApiController.CreateOrder"/> returns Conflict when the order number already exists.
        /// </summary>
        [Fact]
        public async Task CreateOrder_DuplicateOrderNumber_ReturnsConflict()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var erpNumber = "DUPLICATE-001";

            using (var context = new ApplicationDbContext(options))
            {
                context.PurchaseOrders.Add(new PurchaseOrder { ErpOrderNumber = erpNumber, SupplierId = Guid.NewGuid(), CampaignId = Guid.NewGuid() });
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var controller = new OrdersApiController(context);
                var request = new CreateOrderRequest { ErpOrderNumber = erpNumber };

                // Act
                var result = await controller.CreateOrder(request);

                // Assert
                Assert.IsType<ConflictObjectResult>(result);
            }
        }
    }
}
