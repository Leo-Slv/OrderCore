using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementReasonAndReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_movements_ProductId",
                table: "stock_movements");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "stock_movements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnedAt",
                table: "inventory_reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ProductId_CreatedAt",
                table: "stock_movements",
                columns: new[] { "ProductId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_movements_ProductId_CreatedAt",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "inventory_reservations");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ProductId",
                table: "stock_movements",
                column: "ProductId");
        }
    }
}
