using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                    QuantityReserved = table.Column<int>(type: "integer", nullable: false),
                    ReorderLevel = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_OrderId",
                table: "inventory_reservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ProductId",
                table: "inventory_reservations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_ProductId",
                table: "stock_items",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ProductId",
                table: "stock_movements",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_reservations");

            migrationBuilder.DropTable(
                name: "stock_items");

            migrationBuilder.DropTable(
                name: "stock_movements");
        }
    }
}
