using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandOrdersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "order_number_seq");

            migrationBuilder.AddColumn<string>(
                name: "BillingCity",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingComplement",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCountry",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingNeighborhood",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingNumber",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingPostalCode",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingState",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingStreet",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNotes",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ShippedAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmount",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShippingCity",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingComplement",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingCountry",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingNeighborhood",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingNumber",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingPostalCode",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingState",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingStreet",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductImageUrl",
                table: "order_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductSku",
                table: "order_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "order_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderNumber",
                table: "orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_OrderId",
                table: "order_status_history",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_status_history");

            migrationBuilder.DropIndex(
                name: "IX_orders_OrderNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingCity",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingComplement",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingCountry",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingNeighborhood",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingPostalCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingState",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BillingStreet",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CustomerNotes",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "InternalNotes",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippedAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingAmount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingCity",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingComplement",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingCountry",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingNeighborhood",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingPostalCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingState",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingStreet",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ProductImageUrl",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ProductSku",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "order_items");

            migrationBuilder.DropSequence(
                name: "order_number_seq");
        }
    }
}
