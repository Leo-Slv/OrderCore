using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRequestedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentRequestedAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            // Orders already waiting for payment count from when they were created.
            migrationBuilder.Sql("UPDATE orders SET \"PaymentRequestedAt\" = \"CreatedAt\" WHERE \"Status\" = 'PendingPayment';");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_PaymentRequestedAt",
                table: "orders",
                columns: new[] { "Status", "PaymentRequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_Status_PaymentRequestedAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentRequestedAt",
                table: "orders");
        }
    }
}
