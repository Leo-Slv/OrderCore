using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentVoid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "payments");
        }
    }
}
