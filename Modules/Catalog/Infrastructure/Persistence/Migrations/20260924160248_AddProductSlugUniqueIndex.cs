using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSlugUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Slugs used to be generated from the name alone, so an existing
            // database may already hold duplicates. Every duplicate except the
            // oldest product gets a short piece of its own id appended
            // (lowercase hex: still a valid slug, still within the
            // 200-character column) before the unique index is created.
            migrationBuilder.Sql(
                """
                UPDATE products AS p
                SET "Slug" = left(p."Slug", 191) || '-' || left(replace(p."Id"::text, '-', ''), 8)
                FROM (
                    SELECT "Id", row_number() OVER (PARTITION BY "Slug" ORDER BY "CreatedAt", "Id") AS position
                    FROM products
                ) AS ranked
                WHERE p."Id" = ranked."Id" AND ranked.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_products_Slug",
                table: "products",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_Slug",
                table: "products");
        }
    }
}
