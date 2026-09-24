using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutIdempotencyKeyAndHistorySequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutIdempotencyKey",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // An identity column can't also have a default; PostgreSQL numbers
            // the rows that already exist as it adds the column.
            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "order_status_history",
                type: "bigint",
                nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId_CheckoutIdempotencyKey",
                table: "orders",
                columns: new[] { "CustomerId", "CheckoutIdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerId_CheckoutIdempotencyKey",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CheckoutIdempotencyKey",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "order_status_history");
        }
    }
}
