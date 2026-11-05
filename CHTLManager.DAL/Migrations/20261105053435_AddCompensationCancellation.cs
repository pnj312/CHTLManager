using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CHTLManager.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "CompensationPayments",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "CompensationPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByUserId",
                table: "CompensationPayments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "CompensationPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationPayments_CancelledByUserId",
                table: "CompensationPayments",
                column: "CancelledByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompensationPayments_Users_CancelledByUserId",
                table: "CompensationPayments",
                column: "CancelledByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompensationPayments_Users_CancelledByUserId",
                table: "CompensationPayments");

            migrationBuilder.DropIndex(
                name: "IX_CompensationPayments_CancelledByUserId",
                table: "CompensationPayments");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "CompensationPayments");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "CompensationPayments");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "CompensationPayments");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "CompensationPayments");
        }
    }
}
