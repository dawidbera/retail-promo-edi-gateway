using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailEdiGateway.Core.Entities;
using RetailEdiGateway.Infrastructure.Persistence;
using RetailEdiGateway.Web.Controllers.Api;
using System;
using System.Threading.Tasks;
using Xunit;

namespace RetailEdiGateway.Tests.Controllers.Api
{
    /// <summary>
    /// Unit tests for the <see cref="LogisticsApiController"/>.
    /// Verifies the WMS callback functionality for updating warehouse slot statuses.
    /// </summary>
    public class LogisticsApiControllerTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        /// <summary>
        /// Verifies that <see cref="LogisticsApiController.UpdateSlotStatus"/> correctly updates the slot status and cascades the "Delivered" state to the PO line when completed.
        /// </summary>
        [Fact]
        public async Task UpdateSlotStatus_CompletedStatus_UpdatesSlotAndPoLine()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var slotId = Guid.NewGuid();
            var poLineId = Guid.NewGuid();

            using (var context = new ApplicationDbContext(options))
            {
                var campaign = new Campaign { Id = Guid.NewGuid(), Name = "Test" };
                var supplier = new Supplier { Id = Guid.NewGuid(), Code = "S1", Name = "Supplier" };
                var po = new PurchaseOrder { Id = Guid.NewGuid(), CampaignId = campaign.Id, SupplierId = supplier.Id, ErpOrderNumber = "PO1" };

                var poLine = new PurchaseOrderLine
                {
                    Id = poLineId,
                    PurchaseOrderId = po.Id,
                    ProductCode = "P1",
                    ProductName = "Prod 1",
                    Status = PurchaseOrderLineStatus.Shipped
                };

                var slot = new WarehouseSlot
                {
                    Id = slotId,
                    PurchaseOrderLineId = poLineId,
                    DcCode = "DC1",
                    BayNumber = "B1",
                    Status = WarehouseSlotStatus.Booked
                };

                context.Campaigns.Add(campaign);
                context.Suppliers.Add(supplier);
                context.PurchaseOrders.Add(po);
                context.PurchaseOrderLines.Add(poLine);
                context.WarehouseSlots.Add(slot);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var controller = new LogisticsApiController(context);
                var request = new WmsSlotUpdateRequest
                {
                    SlotId = slotId,
                    Status = "Completed",
                    BayNumber = "B2" // Test bay re-assignment
                };

                // Act
                var result = await controller.UpdateSlotStatus(request);

                // Assert
                Assert.IsType<OkObjectResult>(result);

                // Verify DB state
                var updatedSlot = await context.WarehouseSlots
                    .Include(s => s.PurchaseOrderLine)
                    .FirstOrDefaultAsync(s => s.Id == slotId);

                Assert.NotNull(updatedSlot);
                Assert.Equal(WarehouseSlotStatus.Completed, updatedSlot.Status);
                Assert.Equal("B2", updatedSlot.BayNumber);
                Assert.Equal(PurchaseOrderLineStatus.Delivered, updatedSlot.PurchaseOrderLine.Status);
            }
        }

        /// <summary>
        /// Verifies that <see cref="LogisticsApiController.UpdateSlotStatus"/> returns NotFound for a non-existent slot ID.
        /// </summary>
        [Fact]
        public async Task UpdateSlotStatus_NonExistentSlot_ReturnsNotFound()
        {
            // Arrange
            using var context = new ApplicationDbContext(CreateNewContextOptions());
            var controller = new LogisticsApiController(context);
            var request = new WmsSlotUpdateRequest { SlotId = Guid.NewGuid(), Status = "Arrived" };

            // Act
            var result = await controller.UpdateSlotStatus(request);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
