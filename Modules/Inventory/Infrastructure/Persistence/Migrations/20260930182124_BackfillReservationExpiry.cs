using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillReservationExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reservations made before they had a lifetime get the same 2 hours.
            migrationBuilder.Sql(
                "UPDATE inventory_reservations SET \"ExpiresAt\" = \"ReservedAt\" + interval '2 hours' " +
                "WHERE \"Status\" = 'Reserved' AND \"ExpiresAt\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_Status_ExpiresAt",
                table: "inventory_reservations",
                columns: new[] { "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_reservations_Status_ExpiresAt",
                table: "inventory_reservations");
        }
    }
}
