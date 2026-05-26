using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RetailEdiGateway.Infrastructure.Persistence.Migrations
{
 /// <inheritdoc />
 public partial class InitialCreate : Migration
 {
 /// <inheritdoc />
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.CreateTable(
 name: "campaigns",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
 StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
 DeliveryDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
 Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_campaigns", x => x.Id);
 });

 migrationBuilder.CreateTable(
 name: "suppliers",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
 IntegrationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_suppliers", x => x.Id);
 });

 migrationBuilder.CreateTable(
 name: "purchase_orders",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
 SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
 ErpOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
 Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_purchase_orders", x => x.Id);
 table.ForeignKey(
 name: "FK_purchase_orders_campaigns_CampaignId",
 column: x => x.CampaignId,
 principalTable: "campaigns",
 principalColumn: "Id",
 onDelete: ReferentialAction.Cascade);
 table.ForeignKey(
 name: "FK_purchase_orders_suppliers_SupplierId",
 column: x => x.SupplierId,
 principalTable: "suppliers",
 principalColumn: "Id",
 onDelete: ReferentialAction.Restrict);
 });

 migrationBuilder.CreateTable(
 name: "edi_transactions",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
 MessageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 Direction = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 Payload = table.Column<string>(type: "text", nullable: false),
 Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
 RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
 ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_edi_transactions", x => x.Id);
 table.ForeignKey(
 name: "FK_edi_transactions_purchase_orders_PurchaseOrderId",
 column: x => x.PurchaseOrderId,
 principalTable: "purchase_orders",
 principalColumn: "Id",
 onDelete: ReferentialAction.SetNull);
 });

 migrationBuilder.CreateTable(
 name: "purchase_order_lines",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
 ProductCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
 ProductName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
 OrderedQty = table.Column<int>(type: "integer", nullable: false),
 ConfirmedQty = table.Column<int>(type: "integer", nullable: false),
 RequestedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
 ConfirmedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
 Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_purchase_order_lines", x => x.Id);
 table.ForeignKey(
 name: "FK_purchase_order_lines_purchase_orders_PurchaseOrderId",
 column: x => x.PurchaseOrderId,
 principalTable: "purchase_orders",
 principalColumn: "Id",
 onDelete: ReferentialAction.Cascade);
 });

 migrationBuilder.CreateTable(
 name: "warehouse_slots",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
 DcCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
 BookedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
 BayNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
 IsSyncedToWms = table.Column<bool>(type: "boolean", nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_warehouse_slots", x => x.Id);
 table.ForeignKey(
 name: "FK_warehouse_slots_purchase_order_lines_PurchaseOrderLineId",
 column: x => x.PurchaseOrderLineId,
 principalTable: "purchase_order_lines",
 principalColumn: "Id",
 onDelete: ReferentialAction.Cascade);
 });

 migrationBuilder.InsertData(
 table: "campaigns",
 columns: new[] { "Id", "DeliveryDeadline", "Name", "StartDate", "Status" },
 values: new object[,]
 {
 { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 5, 28, 23, 59, 59, 0, DateTimeKind.Utc), "Italian Week 2026", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Active" },
 { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 6, 10, 23, 59, 59, 0, DateTimeKind.Utc), "Spring Gardening Week 2026", new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Utc), "Scheduled" }
 });

 migrationBuilder.InsertData(
 table: "suppliers",
 columns: new[] { "Id", "Code", "ContactEmail", "IntegrationType", "Name" },
 values: new object[,]
 {
 { new Guid("33333333-3333-3333-3333-333333333333"), "SUPP-ITA-01", "contact@italianfood.it", "EDIFACT", "Italian Food Distributors S.p.A." },
 { new Guid("44444444-4444-4444-4444-444444444444"), "SUPP-DEO-02", "info@gartendeko.de", "XML", "Garten und Deko GmbH" }
 });

 migrationBuilder.InsertData(
 table: "purchase_orders",
 columns: new[] { "Id", "CampaignId", "CreatedAt", "ErpOrderNumber", "Status", "SupplierId" },
 values: new object[,]
 {
 { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 5, 10, 10, 0, 0, 0, DateTimeKind.Utc), "PO-2026-001", "Sent", new Guid("33333333-3333-3333-3333-333333333333") },
 { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 5, 20, 14, 30, 0, 0, DateTimeKind.Utc), "PO-2026-002", "Draft", new Guid("44444444-4444-4444-4444-444444444444") }
 });

 migrationBuilder.InsertData(
 table: "purchase_order_lines",
 columns: new[] { "Id", "ConfirmedDate", "ConfirmedQty", "OrderedQty", "ProductCode", "ProductName", "PurchaseOrderId", "RequestedDate", "Status" },
 values: new object[,]
 {
 { new Guid("77777777-7777-7777-7777-777777777777"), null, 0, 10000, "8001234567890", "Spaghetti Pasta 500g", new Guid("55555555-5555-5555-5555-555555555555"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Pending" },
 { new Guid("88888888-8888-8888-8888-888888888888"), null, 0, 5000, "8009876543210", "Extra Virgin Olive Oil 1L", new Guid("55555555-5555-5555-5555-555555555555"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Pending" },
 { new Guid("99999999-9999-9999-9999-999999999999"), null, 0, 1500, "4001122334455", "Garden Tool Set", new Guid("66666666-6666-6666-6666-666666666666"), new DateTime(2026, 6, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Pending" }
 });

 migrationBuilder.CreateIndex(
 name: "IX_edi_transactions_PurchaseOrderId",
 table: "edi_transactions",
 column: "PurchaseOrderId");

 migrationBuilder.CreateIndex(
 name: "IX_purchase_order_lines_PurchaseOrderId_ProductCode",
 table: "purchase_order_lines",
 columns: new[] { "PurchaseOrderId", "ProductCode" });

 migrationBuilder.CreateIndex(
 name: "IX_purchase_orders_CampaignId_Status",
 table: "purchase_orders",
 columns: new[] { "CampaignId", "Status" });

 migrationBuilder.CreateIndex(
 name: "IX_purchase_orders_ErpOrderNumber",
 table: "purchase_orders",
 column: "ErpOrderNumber",
 unique: true);

 migrationBuilder.CreateIndex(
 name: "IX_purchase_orders_SupplierId",
 table: "purchase_orders",
 column: "SupplierId");

 migrationBuilder.CreateIndex(
 name: "IX_suppliers_Code",
 table: "suppliers",
 column: "Code",
 unique: true);

 migrationBuilder.CreateIndex(
 name: "IX_warehouse_slots_PurchaseOrderLineId",
 table: "warehouse_slots",
 column: "PurchaseOrderLineId");
 }

 /// <inheritdoc />
 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.DropTable(
 name: "edi_transactions");

 migrationBuilder.DropTable(
 name: "warehouse_slots");

 migrationBuilder.DropTable(
 name: "purchase_order_lines");

 migrationBuilder.DropTable(
 name: "purchase_orders");

 migrationBuilder.DropTable(
 name: "campaigns");

 migrationBuilder.DropTable(
 name: "suppliers");
 }
 }
}
