using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMessagingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "failed_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    TraceParent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TraceState = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    FirstFailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failed_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_failed_messages_MessageId",
                table: "failed_messages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_failed_messages_Status_LastFailedAt",
                table: "failed_messages",
                columns: new[] { "Status", "LastFailedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "failed_messages");
        }
    }
}
