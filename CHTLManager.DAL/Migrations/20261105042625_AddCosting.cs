using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CHTLManager.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "StockMovements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCost",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LastCost",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "CostIsEstimated",
                table: "OrderDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "OrderDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Hóa đơn cũ không có giá vốn: đánh dấu tạm tính để báo cáo không coi lãi là chắc chắn.
            migrationBuilder.Sql("""UPDATE "OrderDetails" SET "CostIsEstimated" = 1""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "AverageCost",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastCost",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CostIsEstimated",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "OrderDetails");
        }
    }
}
