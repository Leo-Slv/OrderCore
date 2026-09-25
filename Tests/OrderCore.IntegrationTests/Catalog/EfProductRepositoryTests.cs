using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Catalog;

/// <summary>
/// Same shape as EfCustomerRepositoryTests: exercises the real PostgreSQL
/// provider and the InitialCatalogSchema migration, not an in-memory
/// provider.
/// </summary>
public sealed class EfProductRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var dbContext = new CatalogDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private CatalogDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task AddAsync_then_GetBySkuAsync_round_trips_a_product_with_an_image_and_variant()
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.Empty;

        await using (var dbContext = CreateDbContext())
        {
            var categoryRepository = new EfCategoryRepository(dbContext);
            var category = Category.Create("Electronics", Slug.Create("electronics"), null, null, DateTimeOffset.UtcNow);
            categoryId = category.Id;
            await categoryRepository.AddAsync(category, CancellationToken.None);
            await categoryRepository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);
            var product = Product.Create("SKU-1", "Wireless Mouse", Slug.Create("wireless-mouse"), categoryId, 99.9m, "BRL", DateTimeOffset.UtcNow);
            product.AddImage("https://example.com/mouse.png", "Mouse", isPrimary: true, DateTimeOffset.UtcNow);
            product.AddVariant("SKU-1-BLK", "Black", "{\"color\":\"black\"}", 0m, DateTimeOffset.UtcNow);

            await repository.AddAsync(product, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
            productId = product.Id;
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);
            var reloaded = await repository.GetBySkuAsync("SKU-1", CancellationToken.None);

            reloaded.Should().NotBeNull();
            reloaded!.Id.Should().Be(productId);
            reloaded.Images.Should().ContainSingle(i => i.IsPrimary);
            reloaded.Variants.Should().ContainSingle(v => v.Sku == "SKU-1-BLK");
        }
    }

    [Fact]
    public async Task Publishing_a_loaded_product_and_saving_persists_the_status()
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.Empty;

        await using (var dbContext = CreateDbContext())
        {
            var categoryRepository = new EfCategoryRepository(dbContext);
            var category = Category.Create("Electronics", Slug.Create("electronics"), null, null, DateTimeOffset.UtcNow);
            categoryId = category.Id;
            await categoryRepository.AddAsync(category, CancellationToken.None);
            await categoryRepository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);
            var product = Product.Create("SKU-2", "Widget", Slug.Create("widget"), categoryId, 10m, "BRL", DateTimeOffset.UtcNow);
            await repository.AddAsync(product, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
            productId = product.Id;
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);
            var product = await repository.GetByIdAsync(productId, CancellationToken.None);
            product!.Publish(DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);
            var reloaded = await repository.GetByIdAsync(productId, CancellationToken.None);

            reloaded!.Status.Should().Be(Api.Modules.Catalog.Domain.Enums.ProductStatus.Active);
        }
    }

    private static async Task<Guid> SeedAsync(
        CatalogDbContext dbContext, string sku, string name, decimal price, decimal? compareAtPrice = null, DateTimeOffset? publishedAt = null)
    {
        var repository = new EfProductRepository(dbContext);
        var product = Product.Create(sku, name, Slug.GenerateFrom(name), Guid.NewGuid(), price, "BRL", DateTimeOffset.UtcNow);
        await repository.AddAsync(product, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // No domain method sets CompareAtPrice or back-dates PublishedAt,
        // so both are written straight to the row.
        await dbContext.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.CompareAtPrice, compareAtPrice)
                .SetProperty(p => p.PublishedAt, publishedAt));

        return product.Id;
    }

    [Fact]
    public async Task ListAsync_returns_one_page_and_the_total_across_pages()
    {
        await using (var dbContext = CreateDbContext())
        {
            await SeedAsync(dbContext, "SKU-A", "Alpha", 10m);
            await SeedAsync(dbContext, "SKU-B", "Bravo", 20m);
            await SeedAsync(dbContext, "SKU-C", "Charlie", 30m);
        }

        await using (var dbContext = CreateDbContext())
        {
            var (items, totalCount) = await new EfProductRepository(dbContext)
                .ListAsync(new ListProductsFilter { Page = 2, PageSize = 2 }, publishedOnly: false, CancellationToken.None);

            totalCount.Should().Be(3);
            items.Select(p => p.Name).Should().Equal("Charlie");
        }
    }

    [Theory]
    [InlineData(ProductSortOrder.Name, new[] { "Alpha", "Bravo", "Charlie" })]
    [InlineData(ProductSortOrder.PriceAsc, new[] { "Bravo", "Charlie", "Alpha" })]
    [InlineData(ProductSortOrder.PriceDesc, new[] { "Alpha", "Charlie", "Bravo" })]
    [InlineData(ProductSortOrder.Newest, new[] { "Charlie", "Alpha", "Bravo" })]
    public async Task ListAsync_applies_the_requested_sort(ProductSortOrder sort, string[] expectedNames)
    {
        var now = DateTimeOffset.UtcNow;
        await using (var dbContext = CreateDbContext())
        {
            await SeedAsync(dbContext, "SKU-A", "Alpha", 30m, publishedAt: now.AddDays(-2));
            await SeedAsync(dbContext, "SKU-B", "Bravo", 10m, publishedAt: null);
            await SeedAsync(dbContext, "SKU-C", "Charlie", 20m, publishedAt: now.AddDays(-1));
        }

        await using (var dbContext = CreateDbContext())
        {
            var (items, _) = await new EfProductRepository(dbContext)
                .ListAsync(new ListProductsFilter { Sort = sort }, publishedOnly: false, CancellationToken.None);

            items.Select(p => p.Name).Should().Equal(expectedNames);
        }
    }

    [Fact]
    public async Task ListAsync_with_on_sale_keeps_only_products_priced_below_their_compare_at_price()
    {
        await using (var dbContext = CreateDbContext())
        {
            await SeedAsync(dbContext, "SKU-A", "Alpha", 10m, compareAtPrice: 15m);
            await SeedAsync(dbContext, "SKU-B", "Bravo", 10m, compareAtPrice: 10m);
            await SeedAsync(dbContext, "SKU-C", "Charlie", 10m);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);

            var (onSale, onSaleCount) = await repository.ListAsync(
                new ListProductsFilter { OnSale = true }, publishedOnly: false, CancellationToken.None);
            var (notOnSale, _) = await repository.ListAsync(
                new ListProductsFilter { OnSale = false }, publishedOnly: false, CancellationToken.None);

            onSaleCount.Should().Be(1);
            onSale.Select(p => p.Name).Should().Equal("Alpha");
            notOnSale.Select(p => p.Name).Should().Equal("Bravo", "Charlie");
        }
    }

    [Fact]
    public async Task GetBySlugAsync_finds_the_product_with_that_slug()
    {
        Guid productId;
        await using (var dbContext = CreateDbContext())
        {
            productId = await SeedAsync(dbContext, "SKU-A", "Wireless Mouse", 10m);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);

            (await repository.GetBySlugAsync(Slug.Create("wireless-mouse"), CancellationToken.None))!.Id.Should().Be(productId);
            (await repository.GetBySlugAsync(Slug.Create("unknown"), CancellationToken.None)).Should().BeNull();
        }
    }

    [Fact]
    public async Task Two_products_cannot_share_a_slug()
    {
        await using (var dbContext = CreateDbContext())
        {
            await SeedAsync(dbContext, "SKU-A", "Wireless Mouse", 10m);
        }

        await using (var dbContext = CreateDbContext())
        {
            var act = () => SeedAsync(dbContext, "SKU-B", "Wireless Mouse", 20m);

            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Slug_index_migration_renames_existing_duplicates_before_creating_the_index()
    {
        await using var dbContext = CreateDbContext();
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync("20260923162046_InitialCatalogSchema");

        var oldest = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var newer = Guid.Parse("bbbbbbbb-1111-0000-0000-000000000002");
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO products ("Id", "Sku", "Name", "Slug", "CategoryId", "CurrentPrice", "Currency", "Status", "Active", "CreatedAt", "UpdatedAt", "Version")
            VALUES
                ({0}, 'SKU-A', 'Mouse', 'mouse', {2}, 10, 'BRL', 'Draft', true, now() - interval '1 day', now(), 1),
                ({1}, 'SKU-B', 'Mouse', 'mouse', {2}, 20, 'BRL', 'Draft', true, now(), now(), 1);
            """,
            oldest, newer, Guid.NewGuid());

        await migrator.MigrateAsync();

        var slugs = await dbContext.Products.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Slug);
        slugs[oldest].Should().Be("mouse");
        slugs[newer].Should().Be("mouse-bbbbbbbb");
    }

    [Fact]
    public async Task Adding_an_image_and_variant_to_an_already_saved_product_inserts_them()
    {
        Guid productId;
        await using (var dbContext = CreateDbContext())
        {
            productId = await SeedAsync(dbContext, "SKU-A", "Wireless Mouse", 10m);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);
            var product = await repository.GetByIdAsync(productId, CancellationToken.None);
            product!.AddImage("https://example.com/mouse.png", "Mouse", isPrimary: true, DateTimeOffset.UtcNow);
            product.AddVariant("SKU-A-BLK", "Black", "{\"color\":\"black\"}", 0m, DateTimeOffset.UtcNow);

            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var reloaded = await new EfProductRepository(dbContext).GetByIdAsync(productId, CancellationToken.None);

            reloaded!.Images.Should().ContainSingle();
            reloaded.Variants.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task ListAsync_published_only_ignores_drafts_and_deactivated_products_whatever_the_filter_says()
    {
        Guid publishedId;
        await using (var dbContext = CreateDbContext())
        {
            publishedId = await SeedAsync(dbContext, "SKU-A", "Published", 10m);
            await SeedAsync(dbContext, "SKU-B", "Draft", 10m);
            await dbContext.Products
                .Where(p => p.Id == publishedId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Active"));
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfProductRepository(dbContext);

            var (published, publishedCount) = await repository.ListAsync(
                new ListProductsFilter { Active = false }, publishedOnly: true, CancellationToken.None);
            var (all, _) = await repository.ListAsync(new ListProductsFilter(), publishedOnly: false, CancellationToken.None);

            publishedCount.Should().Be(0, "the filter's Active=false can't widen what a published-only listing shows");
            published.Should().BeEmpty();
            all.Should().HaveCount(2);

            var (onlyPublished, _) = await repository.ListAsync(new ListProductsFilter(), publishedOnly: true, CancellationToken.None);
            onlyPublished.Select(p => p.Id).Should().Equal(publishedId);
        }
    }
}
