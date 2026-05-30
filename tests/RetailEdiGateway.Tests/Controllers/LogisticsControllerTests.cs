using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using RetailEdiGateway.Application.Features.Logistics.Commands;
using RetailEdiGateway.Core.Entities;
using RetailEdiGateway.Infrastructure.Persistence;
using RetailEdiGateway.Web.Controllers;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RetailEdiGateway.Tests.Controllers
{
    /// <summary>
    /// Unit and Integration tests for the <see cref="LogisticsController"/>.
    /// Verifies data retrieval for the logistics view and slot booking submission.
    /// </summary>
    public class LogisticsControllerTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        /// <summary>
        /// Verifies that <see cref="LogisticsController.Index"/> fetches slots and pending lines for the view.
        /// </summary>
        [Fact]
        public async Task Index_ReturnsViewWithData()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var supplierId = Guid.NewGuid();
            var campaignId = Guid.NewGuid();

            using (var context = new ApplicationDbContext(options))
            {
                var supplier = new Supplier { Id = supplierId, Code = "S1", Name = "Supplier 1" };
                var campaign = new Campaign { Id = campaignId, Name = "C1", Status = "Active" };
                var po = new PurchaseOrder { Id = Guid.NewGuid(), ErpOrderNumber = "PO1", SupplierId = supplierId, CampaignId = campaignId };
                
                var line1 = new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, ProductCode = "P1", ProductName = "Prod 1" };
                var line2 = new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, ProductCode = "P2", ProductName = "Prod 2" };
                
                var slot = new WarehouseSlot { PurchaseOrderLineId = line1.Id, DcCode = "DC1", BayNumber = "B1", Status = WarehouseSlotStatus.Booked };

                context.Suppliers.Add(supplier);
                context.Campaigns.Add(campaign);
                context.PurchaseOrders.Add(po);
                context.PurchaseOrderLines.AddRange(line1, line2);
                context.WarehouseSlots.Add(slot);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var mockMediator = new Mock<IMediator>();
                var controller = new LogisticsController(mockMediator.Object, context);

                // Act
                var result = await controller.Index();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.NotNull(controller.ViewBag.Slots);
                Assert.NotNull(controller.ViewBag.PendingLines);
                
                var slots = (System.Collections.Generic.List<WarehouseSlot>)controller.ViewBag.Slots;
                var pending = (System.Collections.Generic.List<PurchaseOrderLine>)controller.ViewBag.PendingLines;

                Assert.Single(slots);
                Assert.Single(pending);
            }
        }

        /// <summary>
        /// Verifies that <see cref="LogisticsController.Book"/> dispatches the command and handles success response.
        /// </summary>
        [Fact]
        public async Task Book_ValidCommand_RedirectsWithSuccess()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options);
            var mockMediator = new Mock<IMediator>();
            
            // Initialize TempData for the controller
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
            
            var controller = new LogisticsController(mockMediator.Object, context)
            {
                TempData = tempData
            };

            var poLineId = Guid.NewGuid();
            var response = new WarehouseSlotResponseDto { Success = true, Message = "Success!" };

            mockMediator.Setup(m => m.Send(It.IsAny<BookWarehouseSlotCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            var result = await controller.Book(poLineId, "DC1", DateTime.UtcNow, "B1");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Success!", controller.TempData["SuccessMessage"]);
        }
    }
}
