using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Payments' outbox becomes the shared messaging shape
    /// (Docs/specs/events/async-messaging-implementation-plan.md, stage 2).
    /// Hand-written as a rename (EF scaffolded a drop and create) so rows not
    /// yet published survive: their CLR type names become contract names and
    /// the Messaging relay publishes them.
    /// </summary>
    public partial class MoveOutboxToSharedMessaging : Migration
    {
        private static readonly (string Old, string Contract)[] Types =
        [
            ("PaymentRequested", "payments.payment-requested"),
            ("PaymentAuthorized", "payments.payment-authorized"),
            ("PaymentFailed", "payments.payment-failed"),
            ("PaymentRefunded", "payments.payment-refunded"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_outbox_messages_ProcessedAt", table: "outbox_messages");
            migrationBuilder.DropPrimaryKey(name: "PK_outbox_messages", table: "outbox_messages");
            migrationBuilder.RenameTable(name: "outbox_messages", newName: "payments_outbox_messages");
            migrationBuilder.RenameColumn(name: "ProcessedAt", table: "payments_outbox_messages", newName: "SentAt");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "payments_outbox_messages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "Version", table: "payments_outbox_messages", type: "integer", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<string>(
                name: "TraceParent", table: "payments_outbox_messages", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "TraceState", table: "payments_outbox_messages", type: "character varying(512)", maxLength: 512, nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "CausationId", table: "payments_outbox_messages", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "PublishAttempts", table: "payments_outbox_messages", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(
                name: "LastError", table: "payments_outbox_messages", type: "character varying(2000)", maxLength: 2000, nullable: true);

            foreach (var (old, contract) in Types)
            {
                migrationBuilder.Sql(
                    $"UPDATE payments_outbox_messages SET \"Type\" = '{contract}' WHERE \"Type\" = '{old}';");
            }

            migrationBuilder.AddPrimaryKey(name: "PK_payments_outbox_messages", table: "payments_outbox_messages", column: "Id");
            migrationBuilder.CreateIndex(
                name: "IX_payments_outbox_messages_OccurredAt",
                table: "payments_outbox_messages",
                column: "OccurredAt",
                filter: "\"SentAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_payments_outbox_messages_OccurredAt", table: "payments_outbox_messages");
            migrationBuilder.DropPrimaryKey(name: "PK_payments_outbox_messages", table: "payments_outbox_messages");

            foreach (var (old, contract) in Types)
            {
                migrationBuilder.Sql(
                    $"UPDATE payments_outbox_messages SET \"Type\" = '{old}' WHERE \"Type\" = '{contract}';");
            }

            // Captured/voided events didn't exist before; the old publisher couldn't read them.
            migrationBuilder.Sql(
                "DELETE FROM payments_outbox_messages WHERE \"Type\" IN ('payments.payment-captured', 'payments.payment-voided');");

            migrationBuilder.DropColumn(name: "Version", table: "payments_outbox_messages");
            migrationBuilder.DropColumn(name: "TraceParent", table: "payments_outbox_messages");
            migrationBuilder.DropColumn(name: "TraceState", table: "payments_outbox_messages");
            migrationBuilder.DropColumn(name: "CausationId", table: "payments_outbox_messages");
            migrationBuilder.DropColumn(name: "PublishAttempts", table: "payments_outbox_messages");
            migrationBuilder.DropColumn(name: "LastError", table: "payments_outbox_messages");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "payments_outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.RenameColumn(name: "SentAt", table: "payments_outbox_messages", newName: "ProcessedAt");
            migrationBuilder.RenameTable(name: "payments_outbox_messages", newName: "outbox_messages");
            migrationBuilder.AddPrimaryKey(name: "PK_outbox_messages", table: "outbox_messages", column: "Id");
            migrationBuilder.CreateIndex(name: "IX_outbox_messages_ProcessedAt", table: "outbox_messages", column: "ProcessedAt");
        }
    }
}
