using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CHTLManager.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddLotCostBaseStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostBaseStock",
                table: "PurchaseReceiptLines",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostBaseStock",
                table: "PurchaseReceiptLines");
        }
    }
}
