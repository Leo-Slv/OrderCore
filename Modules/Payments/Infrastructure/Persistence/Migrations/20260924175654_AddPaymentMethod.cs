using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing payments predate the choice of method; Card was the
            // only one the system had, and an empty string would not map back
            // to PaymentMethod when the row is loaded.
            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Card");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Method",
                table: "payments");
        }
    }
}
