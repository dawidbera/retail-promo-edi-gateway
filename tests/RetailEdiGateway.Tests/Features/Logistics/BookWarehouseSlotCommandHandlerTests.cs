using Microsoft.EntityFrameworkCore;
using RetailEdiGateway.Application.Features.Logistics.Commands;
using RetailEdiGateway.Core.Entities;
using RetailEdiGateway.Infrastructure.Persistence;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RetailEdiGateway.Tests.Features.Logistics
{
 /// <summary>
 /// Unit tests for the <see cref="BookWarehouseSlotCommandHandler"/> class.
 /// Verifies booking success, validation checks, and collision warning logic.
 /// </summary>
 public class BookWarehouseSlotCommandHandlerTests
 {
 /// <summary>
 /// Creates new EF Core DbContextOptions configured for a unique isolated in-memory database to allow parallel test execution.
 /// </summary>
 /// <returns>A configured DbContextOptions instance.</returns>
 private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
 {
 // Use unique database names to isolate test executions
 return new DbContextOptionsBuilder<ApplicationDbContext>()
 .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
 .Options;
 }

 /// <summary>
 /// Verifies that trying to book a slot for an invalid PO line returns an error response.
 /// </summary>
 [Fact]
 public async Task Handle_InvalidPoLineId_ReturnsFailureResponse()
 {
 // Arrange
 using var context = new ApplicationDbContext(CreateNewContextOptions());
 var handler = new BookWarehouseSlotCommandHandler(context);
 var command = new BookWarehouseSlotCommand
 {
 PurchaseOrderLineId = Guid.NewGuid(),
 DcCode = "DC-LND1",
 BookedTime = DateTime.UtcNow,
 BayNumber = "Bay 01"
 };

 // Act
 var result = await handler.Handle(command, CancellationToken.None);

 // Assert
 Assert.False(result.Success);
 Assert.Contains("not found", result.Message);
 Assert.Null(result.SlotId);
 }

 /// <summary>
 /// Verifies that booking a slot with no overlapping traffic succeeds with no warnings.
 /// </summary>
 [Fact]
 public async Task Handle_ValidBookingWithoutConflict_SavesSlotAndReturnsSuccess()
 {
 // Arrange
 var options = CreateNewContextOptions();
 var poLineId = Guid.NewGuid();
 var supplierId = Guid.NewGuid();

 using (var context = new ApplicationDbContext(options))
 {
 var supplier = new Supplier { Id = supplierId, Code = "SUPP-01", Name = "Supplier One" };
 var campaign = new Campaign { Id = Guid.NewGuid(), Name = "Test Campaign", Status = "Active" };
 var order = new PurchaseOrder { Id = Guid.NewGuid(), CampaignId = campaign.Id, SupplierId = supplierId, ErpOrderNumber = "PO-100" };
 var line = new PurchaseOrderLine
 {
 Id = poLineId,
 PurchaseOrderId = order.Id,
 ProductCode = "111",
 ProductName = "Product 111",
 OrderedQty = 100
 };

 context.Suppliers.Add(supplier);
 context.Campaigns.Add(campaign);
 context.PurchaseOrders.Add(order);
 context.PurchaseOrderLines.Add(line);
 await context.SaveChangesAsync();
 }

 using (var context = new ApplicationDbContext(options))
 {
 var handler = new BookWarehouseSlotCommandHandler(context);
 var command = new BookWarehouseSlotCommand
 {
 PurchaseOrderLineId = poLineId,
 DcCode = "DC-LND1",
 BookedTime = new DateTime(2026, 05, 28, 10, 0, 0, DateTimeKind.Utc),
 BayNumber = "Bay 01"
 };

 // Act
 var result = await handler.Handle(command, CancellationToken.None);

 // Assert
 Assert.True(result.Success);
 Assert.NotNull(result.SlotId);
 Assert.False(result.HasCollisionWarning);
 Assert.Null(result.CollisionDetails);

 // Verify saved in DB
 var slot = await context.WarehouseSlots.FindAsync(result.SlotId);
 Assert.NotNull(slot);
 Assert.Equal("DC-LND1", slot.DcCode);
 Assert.Equal("Bay 01", slot.BayNumber);
 Assert.Equal(WarehouseSlotStatus.Booked, slot.Status);
 }
 }

 /// <summary>
 /// Verifies that booking a slot overlapping with a different supplier's slot triggers a warning.
 /// </summary>
 [Fact]
 public async Task Handle_BookingWithOverlapDifferentSupplier_ReturnsSuccessWithCollisionWarning()
 {
 // Arrange
 var options = CreateNewContextOptions();
 var supplierAId = Guid.NewGuid();
 var supplierBId = Guid.NewGuid();
 var poLineAId = Guid.NewGuid();
 var poLineBId = Guid.NewGuid();

 using (var context = new ApplicationDbContext(options))
 {
 var supplierA = new Supplier { Id = supplierAId, Code = "SUPP-A", Name = "Supplier A" };
 var supplierB = new Supplier { Id = supplierBId, Code = "SUPP-B", Name = "Supplier B" };
 var campaign = new Campaign { Id = Guid.NewGuid(), Name = " Campaign", Status = "Active" };
 
 var orderA = new PurchaseOrder { Id = Guid.NewGuid(), CampaignId = campaign.Id, SupplierId = supplierAId, ErpOrderNumber = "PO-A" };
 var orderB = new PurchaseOrder { Id = Guid.NewGuid(), CampaignId = campaign.Id, SupplierId = supplierBId, ErpOrderNumber = "PO-B" };

 var lineA = new PurchaseOrderLine { Id = poLineAId, PurchaseOrderId = orderA.Id, ProductCode = "101", ProductName = "Item A", OrderedQty = 50 };
 var lineB = new PurchaseOrderLine { Id = poLineBId, PurchaseOrderId = orderB.Id, ProductCode = "102", ProductName = "Item B", OrderedQty = 30 };

 context.Suppliers.AddRange(supplierA, supplierB);
 context.Campaigns.Add(campaign);
 context.PurchaseOrders.AddRange(orderA, orderB);
 context.PurchaseOrderLines.AddRange(lineA, lineB);
 await context.SaveChangesAsync();

 // Seed existing slot booked for Supplier A at 10:00 AM on Bay 01
 var existingSlot = new WarehouseSlot
 {
 PurchaseOrderLineId = poLineAId,
 DcCode = "DC-LND1",
 BookedTime = new DateTime(2026, 05, 28, 10, 0, 0, DateTimeKind.Utc),
 BayNumber = "Bay 01",
 Status = WarehouseSlotStatus.Booked
 };
 context.WarehouseSlots.Add(existingSlot);
 await context.SaveChangesAsync();
 }

 using (var context = new ApplicationDbContext(options))
 {
 var handler = new BookWarehouseSlotCommandHandler(context);
 
 // Attempt to book for Supplier B at 10:15 AM (15 mins overlap) on same DC and Bay
 var command = new BookWarehouseSlotCommand
 {
 PurchaseOrderLineId = poLineBId,
 DcCode = "DC-LND1",
 BookedTime = new DateTime(2026, 05, 28, 10, 15, 0, DateTimeKind.Utc),
 BayNumber = "Bay 01"
 };

 // Act
 var result = await handler.Handle(command, CancellationToken.None);

 // Assert
 Assert.True(result.Success);
 Assert.True(result.HasCollisionWarning);
 Assert.NotNull(result.CollisionDetails);
 Assert.Contains("Supplier 'Supplier A'", result.CollisionDetails);
 Assert.Contains("PO-A", result.CollisionDetails);
 }
 }

 /// <summary>
 /// Verifies that booking multiple slots overlapping for the SAME supplier does not trigger collision warnings.
 /// </summary>
 [Fact]
 public async Task Handle_BookingWithOverlapSameSupplier_SavesWithoutCollisionWarning()
 {
 // Arrange
 var options = CreateNewContextOptions();
 var supplierId = Guid.NewGuid();
 var poLineAId = Guid.NewGuid();
 var poLineBId = Guid.NewGuid();

 using (var context = new ApplicationDbContext(options))
 {
 var supplier = new Supplier { Id = supplierId, Code = "SUPP-01", Name = "Single Supplier" };
 var campaign = new Campaign { Id = Guid.NewGuid(), Name = " Campaign", Status = "Active" };
 
 var order = new PurchaseOrder { Id = Guid.NewGuid(), CampaignId = campaign.Id, SupplierId = supplierId, ErpOrderNumber = "PO-A" };

 var lineA = new PurchaseOrderLine { Id = poLineAId, PurchaseOrderId = order.Id, ProductCode = "101", ProductName = "Item A", OrderedQty = 50 };
 var lineB = new PurchaseOrderLine { Id = poLineBId, PurchaseOrderId = order.Id, ProductCode = "102", ProductName = "Item B", OrderedQty = 30 };

 context.Suppliers.Add(supplier);
 context.Campaigns.Add(campaign);
 context.PurchaseOrders.Add(order);
 context.PurchaseOrderLines.AddRange(lineA, lineB);
 await context.SaveChangesAsync();

 // Seed existing slot for same supplier at 10:00 AM on Bay 01
 var existingSlot = new WarehouseSlot
 {
 PurchaseOrderLineId = poLineAId,
 DcCode = "DC-LND1",
 BookedTime = new DateTime(2026, 05, 28, 10, 0, 0, DateTimeKind.Utc),
 BayNumber = "Bay 01",
 Status = WarehouseSlotStatus.Booked
 };
 context.WarehouseSlots.Add(existingSlot);
 await context.SaveChangesAsync();
 }

 using (var context = new ApplicationDbContext(options))
 {
 var handler = new BookWarehouseSlotCommandHandler(context);
 
 // Attempt to book line B at 10:15 AM (same supplier)
 var command = new BookWarehouseSlotCommand
 {
 PurchaseOrderLineId = poLineBId,
 DcCode = "DC-LND1",
 BookedTime = new DateTime(2026, 05, 28, 10, 15, 0, DateTimeKind.Utc),
 BayNumber = "Bay 01"
 };

 // Act
 var result = await handler.Handle(command, CancellationToken.None);

 // Assert
 Assert.True(result.Success);
 Assert.False(result.HasCollisionWarning);
 }
 }
 }
}
