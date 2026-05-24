using Microsoft.EntityFrameworkCore;
using Moq;
using RetailPromoEdiGateway.Application.Common.Interfaces;
using RetailPromoEdiGateway.Application.Features.Edi.Commands;
using RetailPromoEdiGateway.Core.Entities;
using RetailPromoEdiGateway.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RetailPromoEdiGateway.Tests.Features.Edi
{
    /// <summary>
    /// Unit tests for the <see cref="ProcessInboundEdiCommandHandler"/> class.
    /// Verifies processing of supplier responses and shipping notifications, including state transitions.
    /// </summary>
    public class ProcessInboundEdiCommandHandlerTests
    {
        private readonly Mock<IEdiParser> _mockEdiParser;
        private readonly Guid _supplierId = Guid.NewGuid();
        private readonly string _supplierCode = "SUPP-ITA-01";
        private readonly Guid _campaignId = Guid.NewGuid();
        private readonly DateTime _deliveryDeadline = new DateTime(2026, 05, 28, 23, 59, 59, DateTimeKind.Utc);

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessInboundEdiCommandHandlerTests"/> class.
        /// </summary>
        public ProcessInboundEdiCommandHandlerTests()
        {
            _mockEdiParser = new Mock<IEdiParser>();
        }

        /// <summary>
        /// Creates new EF Core DbContextOptions configured for a unique isolated in-memory database to allow parallel test execution.
        /// </summary>
        /// <returns>A configured DbContextOptions instance.</returns>
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        /// <summary>
        /// Seed base database configuration (supplier, active campaign, and initial purchase order) for unit testing.
        /// </summary>
        /// <param name="context">The database context to populate.</param>
        private async Task SeedBaseDataAsync(ApplicationDbContext context)
        {
            var supplier = new Supplier
            {
                Id = _supplierId,
                Code = _supplierCode,
                Name = "Italian Food S.p.A.",
                IntegrationType = "EDIFACT"
            };

            var campaign = new Campaign
            {
                Id = _campaignId,
                Name = "Tydzień Włoski 2026",
                StartDate = new DateTime(2026, 06, 01, 0, 0, 0, DateTimeKind.Utc),
                DeliveryDeadline = _deliveryDeadline,
                Status = "Active"
            };

            var po = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                CampaignId = _campaignId,
                SupplierId = _supplierId,
                ErpOrderNumber = "PO-2026-001",
                Status = PurchaseOrderStatus.Sent,
                CreatedAt = DateTime.UtcNow
            };

            po.Lines.Add(new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductCode = "EAN1",
                ProductName = "Spaghetti",
                OrderedQty = 1000,
                ConfirmedQty = 0,
                RequestedDate = new DateTime(2026, 05, 28, 0, 0, 0, DateTimeKind.Utc),
                Status = PurchaseOrderLineStatus.Pending
            });

            context.Suppliers.Add(supplier);
            context.Campaigns.Add(campaign);
            context.PurchaseOrders.Add(po);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Verifies that processing an EDI transaction for a non-existent supplier returns a failure.
        /// </summary>
        [Fact]
        public async Task Handle_NonExistentSupplier_ReturnsFailure()
        {
            // Arrange
            using var context = new ApplicationDbContext(CreateNewContextOptions());
            var handler = new ProcessInboundEdiCommandHandler(context, _mockEdiParser.Object);
            var command = new ProcessInboundEdiCommand
            {
                Payload = "RAW_PAYLOAD",
                MessageType = "ORDRSP",
                SupplierCode = "NON-EXISTENT"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        /// <summary>
        /// Verifies that a valid ORDRSP matching ordered quantities and dates updates the PO and lines status to Confirmed.
        /// </summary>
        [Fact]
        public async Task Handle_ValidOrdrspMatchingQuantities_UpdatesStatusToConfirmed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                await SeedBaseDataAsync(context);
            }

            var ordrspResult = new EdiResponseParseResult
            {
                ErpOrderNumber = "PO-2026-001",
                SupplierCode = _supplierCode,
                Lines = new List<EdiResponseLine>
                {
                    new() { ProductCode = "EAN1", ConfirmedQty = 1000, ConfirmedDate = new DateTime(2026, 05, 28, 0, 0, 0, DateTimeKind.Utc) }
                }
            };

            _mockEdiParser.Setup(p => p.ParseOrdrsp(It.IsAny<string>())).Returns(ordrspResult);

            using (var context = new ApplicationDbContext(options))
            {
                var handler = new ProcessInboundEdiCommandHandler(context, _mockEdiParser.Object);
                var command = new ProcessInboundEdiCommand
                {
                    Payload = "RAW_EDIFACT_ORDRSP_PAYLOAD",
                    MessageType = "ORDRSP",
                    SupplierCode = _supplierCode
                };

                // Act
                var result = await handler.Handle(command, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal("PO-2026-001", result.OrderNumber);

                // Verify db updates
                var po = await context.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.ErpOrderNumber == "PO-2026-001");
                Assert.Equal(PurchaseOrderStatus.Confirmed, po.Status);
                Assert.Equal(PurchaseOrderLineStatus.Confirmed, po.Lines.First().Status);
                Assert.Equal(1000, po.Lines.First().ConfirmedQty);

                // Verify transaction log
                var tx = await context.EdiTransactions.FindAsync(result.TransactionId);
                Assert.NotNull(tx);
                Assert.Equal("SUCCESS", tx.Status);
                Assert.Equal("ORDRSP", tx.MessageType);
            }
        }

        /// <summary>
        /// Verifies that an ORDRSP with a confirmed quantity less than the ordered quantity triggers a Mismatched status.
        /// </summary>
        [Fact]
        public async Task Handle_OrdrspWithShortQuantity_UpdatesStatusToMismatched()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                await SeedBaseDataAsync(context);
            }

            var ordrspResult = new EdiResponseParseResult
            {
                ErpOrderNumber = "PO-2026-001",
                SupplierCode = _supplierCode,
                Lines = new List<EdiResponseLine>
                {
                    new() { ProductCode = "EAN1", ConfirmedQty = 800, ConfirmedDate = new DateTime(2026, 05, 28, 0, 0, 0, DateTimeKind.Utc) }
                }
            };

            _mockEdiParser.Setup(p => p.ParseOrdrsp(It.IsAny<string>())).Returns(ordrspResult);

            using (var context = new ApplicationDbContext(options))
            {
                var handler = new ProcessInboundEdiCommandHandler(context, _mockEdiParser.Object);
                var command = new ProcessInboundEdiCommand
                {
                    Payload = "RAW_EDIFACT_ORDRSP_PAYLOAD",
                    MessageType = "ORDRSP",
                    SupplierCode = _supplierCode
                };

                // Act
                var result = await handler.Handle(command, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Contains("Discrepancies detected", result.Message);

                var po = await context.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.ErpOrderNumber == "PO-2026-001");
                Assert.Equal(PurchaseOrderStatus.Mismatched, po.Status);
                Assert.Equal(PurchaseOrderLineStatus.Mismatched, po.Lines.First().Status);
                Assert.Equal(800, po.Lines.First().ConfirmedQty);
            }
        }

        /// <summary>
        /// Verifies that an ORDRSP confirming a delivery date after the campaign deadline triggers a Mismatched status.
        /// </summary>
        [Fact]
        public async Task Handle_OrdrspWithLateConfirmedDate_UpdatesStatusToMismatched()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                await SeedBaseDataAsync(context);
            }

            var ordrspResult = new EdiResponseParseResult
            {
                ErpOrderNumber = "PO-2026-001",
                SupplierCode = _supplierCode,
                Lines = new List<EdiResponseLine>
                {
                    // Delivery deadline is 28th, supplier confirms 29th (Late delivery)
                    new() { ProductCode = "EAN1", ConfirmedQty = 1000, ConfirmedDate = new DateTime(2026, 05, 29, 12, 0, 0, DateTimeKind.Utc) }
                }
            };

            _mockEdiParser.Setup(p => p.ParseOrdrsp(It.IsAny<string>())).Returns(ordrspResult);

            using (var context = new ApplicationDbContext(options))
            {
                var handler = new ProcessInboundEdiCommandHandler(context, _mockEdiParser.Object);
                var command = new ProcessInboundEdiCommand
                {
                    Payload = "RAW_EDIFACT_ORDRSP_PAYLOAD",
                    MessageType = "ORDRSP",
                    SupplierCode = _supplierCode
                };

                // Act
                var result = await handler.Handle(command, CancellationToken.None);

                // Assert
                Assert.True(result.Success);

                var po = await context.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.ErpOrderNumber == "PO-2026-001");
                Assert.Equal(PurchaseOrderStatus.Mismatched, po.Status);
                Assert.Equal(PurchaseOrderLineStatus.Mismatched, po.Lines.First().Status);
            }
        }

        /// <summary>
        /// Verifies that a valid DESADV message updates the PO and matched lines to Shipped.
        /// </summary>
        [Fact]
        public async Task Handle_ValidDesadv_UpdatesStatusToShipped()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                await SeedBaseDataAsync(context);
            }

            var desadvResult = new EdiDesadvParseResult
            {
                ErpOrderNumber = "PO-2026-001",
                SupplierCode = _supplierCode,
                ShipmentId = "SHIP001",
                CarrierName = "DHL Logistics",
                SSCC = "SSCC-123456789012345678",
                Lines = new List<EdiDesadvLine>
                {
                    new() { ProductCode = "EAN1", ShippedQty = 1000 }
                }
            };

            _mockEdiParser.Setup(p => p.ParseDesadv(It.IsAny<string>())).Returns(desadvResult);

            using (var context = new ApplicationDbContext(options))
            {
                var handler = new ProcessInboundEdiCommandHandler(context, _mockEdiParser.Object);
                var command = new ProcessInboundEdiCommand
                {
                    Payload = "RAW_EDIFACT_DESADV_PAYLOAD",
                    MessageType = "DESADV",
                    SupplierCode = _supplierCode
                };

                // Act
                var result = await handler.Handle(command, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Contains("DESADV processed successfully", result.Message);

                var po = await context.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.ErpOrderNumber == "PO-2026-001");
                Assert.Equal(PurchaseOrderStatus.Shipped, po.Status);
                Assert.Equal(PurchaseOrderLineStatus.Shipped, po.Lines.First().Status);
            }
        }
    }
}
