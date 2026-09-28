using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TraceParent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TraceState = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_outbox_messages_OccurredAt",
                table: "orders_outbox_messages",
                column: "OccurredAt",
                filter: "\"SentAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orders_outbox_messages");
        }
    }
}
