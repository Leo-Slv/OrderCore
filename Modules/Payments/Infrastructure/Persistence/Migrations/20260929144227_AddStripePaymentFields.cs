using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStripePaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisputedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastDeclineReason",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDeclinedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payments_processed_messages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments_processed_messages", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_ProviderReference",
                table: "payments",
                column: "ProviderReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments_processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_payments_ProviderReference",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "DisputedAt",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "LastDeclineReason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "LastDeclinedAt",
                table: "payments");
        }
    }
}
