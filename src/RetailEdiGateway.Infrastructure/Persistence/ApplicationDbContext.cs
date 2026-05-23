using Microsoft.EntityFrameworkCore;
using RetailEdiGateway.Core.Entities;
using RetailEdiGateway.Application.Common.Interfaces;
using System;

namespace RetailEdiGateway.Infrastructure.Persistence
{
 /// <summary>
 /// Database Context for the EDI and Supply Chain Gateway using Entity Framework Core.
 /// </summary>
 public class ApplicationDbContext : DbContext, IApplicationDbContext
 {
 /// <summary>
 /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
 /// </summary>
 public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
 : base(options)
 {
 }

 public DbSet<Campaign> Campaigns => Set<Campaign>();
 public DbSet<Supplier> Suppliers => Set<Supplier>();
 public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
 public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
 public DbSet<EdiTransaction> EdiTransactions => Set<EdiTransaction>();
 public DbSet<WarehouseSlot> WarehouseSlots => Set<WarehouseSlot>();

 /// <summary>
 /// Configures database tables, keys, properties, indexes, and cascades using Fluent API, and seeds initial data.
 /// </summary>
 /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);

 // 1. Campaign Mapping
 modelBuilder.Entity<Campaign>(entity =>
 {
 entity.ToTable("campaigns");
 entity.HasKey(e => e.Id);
 entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
 entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
 });

 // 2. Supplier Mapping
 modelBuilder.Entity<Supplier>(entity =>
 {
 entity.ToTable("suppliers");
 entity.HasKey(e => e.Id);
 entity.HasIndex(e => e.Code).IsUnique();
 entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
 entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
 entity.Property(e => e.IntegrationType).IsRequired().HasMaxLength(50);
 entity.Property(e => e.ContactEmail).HasMaxLength(255);
 });

 // 3. PurchaseOrder Mapping
 modelBuilder.Entity<PurchaseOrder>(entity =>
 {
 entity.ToTable("purchase_orders");
 entity.HasKey(e => e.Id);
 entity.HasIndex(e => e.ErpOrderNumber).IsUnique();
 entity.Property(e => e.ErpOrderNumber).IsRequired().HasMaxLength(100);
 entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasConversion<string>();

 entity.HasOne(d => d.Campaign)
 .WithMany(p => p.Orders)
 .HasForeignKey(d => d.CampaignId)
 .OnDelete(DeleteBehavior.Cascade);

 entity.HasOne(d => d.Supplier)
 .WithMany(p => p.Orders)
 .HasForeignKey(d => d.SupplierId)
 .OnDelete(DeleteBehavior.Restrict);

 // Composite Index: campaign_id + status (optimized query on campaigns dashboard)
 entity.HasIndex(e => new { e.CampaignId, e.Status });
 });

 // 4. PurchaseOrderLine Mapping
 modelBuilder.Entity<PurchaseOrderLine>(entity =>
 {
 entity.ToTable("purchase_order_lines");
 entity.HasKey(e => e.Id);
 entity.Property(e => e.ProductCode).IsRequired().HasMaxLength(100);
 entity.Property(e => e.ProductName).IsRequired().HasMaxLength(255);
 entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasConversion<string>();

 entity.HasOne(d => d.PurchaseOrder)
 .WithMany(p => p.Lines)
 .HasForeignKey(d => d.PurchaseOrderId)
 .OnDelete(DeleteBehavior.Cascade);

 // Composite Index: purchase_order_id + product_code
 entity.HasIndex(e => new { e.PurchaseOrderId, e.ProductCode });
 });

 // 5. EdiTransaction Mapping
 modelBuilder.Entity<EdiTransaction>(entity =>
 {
 entity.ToTable("edi_transactions");
 entity.HasKey(e => e.Id);
 entity.Property(e => e.MessageType).IsRequired().HasMaxLength(50);
 entity.Property(e => e.Direction).IsRequired().HasMaxLength(50);
 entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
 entity.Property(e => e.Payload).IsRequired();
 entity.Property(e => e.RetryCount).IsRequired().HasDefaultValue(0);
 entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

 entity.HasOne(d => d.PurchaseOrder)
 .WithMany(p => p.EdiTransactions)
 .HasForeignKey(d => d.PurchaseOrderId)
 .OnDelete(DeleteBehavior.SetNull);
 });

 // 6. WarehouseSlot Mapping
 modelBuilder.Entity<WarehouseSlot>(entity =>
 {
 entity.ToTable("warehouse_slots");
 entity.HasKey(e => e.Id);
 entity.Property(e => e.DcCode).IsRequired().HasMaxLength(100);
 entity.Property(e => e.BayNumber).IsRequired().HasMaxLength(50);
 entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasConversion<string>();

 entity.HasOne(d => d.PurchaseOrderLine)
 .WithMany(p => p.WarehouseSlots)
 .HasForeignKey(d => d.PurchaseOrderLineId)
 .OnDelete(DeleteBehavior.Cascade);
 });

 // Seed Initial Data
 SeedData(modelBuilder);
 }

 /// <summary>
 /// Seed standard static mock data for campaigns, suppliers, purchase orders, and lines for demonstration purposes.
 /// </summary>
 /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
 private static void SeedData(ModelBuilder modelBuilder)
 {
 // Seed Campaigns
 var campaign1Id = new Guid("11111111-1111-1111-1111-111111111111");
 var campaign2Id = new Guid("22222222-2222-2222-2222-222222222222");

 modelBuilder.Entity<Campaign>().HasData(
 new Campaign
 {
 Id = campaign1Id,
 Name = "Italian Week 2026",
 StartDate = new DateTime(2026, 06, 01, 0, 0, 0, DateTimeKind.Utc),
 DeliveryDeadline = new DateTime(2026, 05, 28, 23, 59, 59, DateTimeKind.Utc),
 Status = "Active"
 },
 new Campaign
 {
 Id = campaign2Id,
 Name = "Spring Gardening Week 2026",
 StartDate = new DateTime(2026, 06, 15, 0, 0, 0, DateTimeKind.Utc),
 DeliveryDeadline = new DateTime(2026, 06, 10, 23, 59, 59, DateTimeKind.Utc),
 Status = "Scheduled"
 }
 );

 // Seed Suppliers
 var supplier1Id = new Guid("33333333-3333-3333-3333-333333333333");
 var supplier2Id = new Guid("44444444-4444-4444-4444-444444444444");

 modelBuilder.Entity<Supplier>().HasData(
 new Supplier
 {
 Id = supplier1Id,
 Code = "SUPP-ITA-01",
 Name = "Italian Food Distributors S.p.A.",
 IntegrationType = "EDIFACT",
 ContactEmail = "contact@italianfood.it"
 },
 new Supplier
 {
 Id = supplier2Id,
 Code = "SUPP-DEO-02",
 Name = "Garten und Deko GmbH",
 IntegrationType = "XML",
 ContactEmail = "info@gartendeko.de"
 }
 );

 // Seed Purchase Orders
 var po1Id = new Guid("55555555-5555-5555-5555-555555555555");
 var po2Id = new Guid("66666666-6666-6666-6666-666666666666");

 modelBuilder.Entity<PurchaseOrder>().HasData(
 new PurchaseOrder
 {
 Id = po1Id,
 CampaignId = campaign1Id,
 SupplierId = supplier1Id,
 ErpOrderNumber = "PO-2026-001",
 Status = PurchaseOrderStatus.Sent,
 CreatedAt = new DateTime(2026, 05, 10, 10, 0, 0, DateTimeKind.Utc)
 },
 new PurchaseOrder
 {
 Id = po2Id,
 CampaignId = campaign2Id,
 SupplierId = supplier2Id,
 ErpOrderNumber = "PO-2026-002",
 Status = PurchaseOrderStatus.Draft,
 CreatedAt = new DateTime(2026, 05, 20, 14, 30, 0, DateTimeKind.Utc)
 }
 );

 // Seed Purchase Order Lines
 var line1Id = new Guid("77777777-7777-7777-7777-777777777777");
 var line2Id = new Guid("88888888-8888-8888-8888-888888888888");
 var line3Id = new Guid("99999999-9999-9999-9999-999999999999");

 modelBuilder.Entity<PurchaseOrderLine>().HasData(
 new PurchaseOrderLine
 {
 Id = line1Id,
 PurchaseOrderId = po1Id,
 ProductCode = "8001234567890",
 ProductName = "Spaghetti Pasta 500g",
 OrderedQty = 10000,
 ConfirmedQty = 0,
 RequestedDate = new DateTime(2026, 05, 28, 0, 0, 0, DateTimeKind.Utc),
 Status = PurchaseOrderLineStatus.Pending
 },
 new PurchaseOrderLine
 {
 Id = line2Id,
 PurchaseOrderId = po1Id,
 ProductCode = "8009876543210",
 ProductName = "Extra Virgin Olive Oil 1L",
 OrderedQty = 5000,
 ConfirmedQty = 0,
 RequestedDate = new DateTime(2026, 05, 28, 0, 0, 0, DateTimeKind.Utc),
 Status = PurchaseOrderLineStatus.Pending
 },
 new PurchaseOrderLine
 {
 Id = line3Id,
 PurchaseOrderId = po2Id,
 ProductCode = "4001122334455",
 ProductName = "Garden Tool Set",
 OrderedQty = 1500,
 ConfirmedQty = 0,
 RequestedDate = new DateTime(2026, 06, 10, 0, 0, 0, DateTimeKind.Utc),
 Status = PurchaseOrderLineStatus.Pending
 }
 );
 }
 }
}
