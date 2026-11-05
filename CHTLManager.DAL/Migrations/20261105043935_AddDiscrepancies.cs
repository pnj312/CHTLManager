using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CHTLManager.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscrepancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockDiscrepancyId",
                table: "PurchaseReceipts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    CountedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CountedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BookStock = table.Column<int>(type: "INTEGER", nullable: false),
                    CountedStock = table.Column<int>(type: "INTEGER", nullable: false),
                    Delta = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResponsibleUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponsibleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CompensationAmount = table.Column<decimal>(type: "decimal(18,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDiscrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockDiscrepancies_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDiscrepancies_Users_CountedByUserId",
                        column: x => x.CountedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDiscrepancies_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDiscrepancies_Users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompensationPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StockDiscrepancyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompensationPayments_StockDiscrepancies_StockDiscrepancyId",
                        column: x => x.StockDiscrepancyId,
                        principalTable: "StockDiscrepancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompensationPayments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_StockDiscrepancyId",
                table: "PurchaseReceipts",
                column: "StockDiscrepancyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationPayments_StockDiscrepancyId",
                table: "CompensationPayments",
                column: "StockDiscrepancyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationPayments_UserId",
                table: "CompensationPayments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDiscrepancies_CountedByUserId",
                table: "StockDiscrepancies",
                column: "CountedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDiscrepancies_ProductId",
                table: "StockDiscrepancies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDiscrepancies_ResolvedByUserId",
                table: "StockDiscrepancies",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDiscrepancies_ResponsibleUserId",
                table: "StockDiscrepancies",
                column: "ResponsibleUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReceipts_StockDiscrepancies_StockDiscrepancyId",
                table: "PurchaseReceipts",
                column: "StockDiscrepancyId",
                principalTable: "StockDiscrepancies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Các lần kiểm kê có chênh lệch trước đây thành phiếu đã xử lý, giữ đúng lý do đã chọn.
            migrationBuilder.Sql("""
                INSERT INTO "StockDiscrepancies" ("ProductId", "CountedByUserId", "CountedAt", "BookStock", "CountedStock", "Delta", "UnitCost",
                                                  "Status", "Resolution", "ResolvedByUserId", "ResolvedAt", "Note")
                SELECT "ProductId", "UserId", "CreatedAt", "BeforeStock", "AfterStock", "Delta", 0, 'Resolved',
                       CASE "Reason" WHEN 'Lost' THEN 'NormalLoss' WHEN 'Damaged' THEN 'NormalLoss' WHEN 'Expired' THEN 'NormalLoss'
                                     WHEN 'UnrecordedImport' THEN 'OtherIncome' ELSE 'Miscount' END,
                       "UserId", "CreatedAt",
                       'Dữ liệu cũ: ' || CASE "Reason" WHEN 'Lost' THEN 'Mất' WHEN 'Damaged' THEN 'Hỏng' WHEN 'Expired' THEN 'Hết hạn'
                                                        WHEN 'UnrecordedImport' THEN 'Hàng nhập chưa ghi' ELSE 'Đếm sai trước đó' END
                FROM "StockMovements"
                WHERE "Type" = 'StockCount' AND "Delta" <> 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReceipts_StockDiscrepancies_StockDiscrepancyId",
                table: "PurchaseReceipts");

            migrationBuilder.DropTable(
                name: "CompensationPayments");

            migrationBuilder.DropTable(
                name: "StockDiscrepancies");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReceipts_StockDiscrepancyId",
                table: "PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "StockDiscrepancyId",
                table: "PurchaseReceipts");
        }
    }
}
