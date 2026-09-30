using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_payments_processed_messages_ProcessedAt",
                table: "payments_processed_messages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_payments_outbox_messages_SentAt",
                table: "payments_outbox_messages",
                column: "SentAt",
                filter: "\"SentAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_processed_messages_ProcessedAt",
                table: "payments_processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_payments_outbox_messages_SentAt",
                table: "payments_outbox_messages");
        }
    }
}
