using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
}
