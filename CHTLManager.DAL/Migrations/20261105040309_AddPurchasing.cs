using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CHTLManager.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchaseReceiptId",
                table: "StockMovements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InvoiceKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceiptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PurchaseReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoicedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLines_PurchaseReceipts_PurchaseReceiptId",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "PurchaseReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseReceiptId",
                table: "StockMovements",
                column: "PurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLines_ProductId",
                table: "PurchaseReceiptLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLines_PurchaseReceiptId",
                table: "PurchaseReceiptLines",
                column: "PurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_Code",
                table: "PurchaseReceipts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_ConfirmedByUserId",
                table: "PurchaseReceipts",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_CreatedByUserId",
                table: "PurchaseReceipts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_SupplierId_InvoiceKey",
                table: "PurchaseReceipts",
                columns: new[] { "SupplierId", "InvoiceKey" },
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_NameKey",
                table: "Suppliers",
                column: "NameKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_PurchaseReceipts_PurchaseReceiptId",
                table: "StockMovements",
                column: "PurchaseReceiptId",
                principalTable: "PurchaseReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Nhà cung cấp đã ghi ở các lần nhập kho cũ thành danh sách nhà cung cấp.
            migrationBuilder.Sql("""
                INSERT INTO "Suppliers" ("Name", "NameKey", "IsActive")
                SELECT trim(MIN("Supplier")), lower(trim("Supplier")), 1
                FROM "StockMovements"
                WHERE "Supplier" IS NOT NULL AND trim("Supplier") <> ''
                GROUP BY lower(trim("Supplier"))
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_PurchaseReceipts_PurchaseReceiptId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "PurchaseReceiptLines");

            migrationBuilder.DropTable(
                name: "PurchaseReceipts");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PurchaseReceiptId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseReceiptId",
                table: "StockMovements");
        }
    }
}
