using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

public sealed class CreateProductUseCaseTests
{
    private static async Task<(FakeProductRepository Products, FakeCategoryRepository Categories, Guid CategoryId)> SetupAsync()
    {
        var categories = new FakeCategoryRepository();
        var category = Category.Create("Electronics", Slug.Create("electronics"), null, null, DateTimeOffset.UtcNow);
        await categories.AddAsync(category, CancellationToken.None);

        return (new FakeProductRepository(), categories, category.Id);
    }

    [Fact]
    public async Task ExecuteAsync_creates_a_product_with_a_slug_derived_from_the_name()
    {
        var (products, categories, categoryId) = await SetupAsync();
        var useCase = new CreateProductUseCase(products, categories, new FakeAuditLogService(), TimeProvider.System);

        var output = await useCase.ExecuteAsync(
            new CreateProductCommand("SKU-1", "Wireless Mouse", categoryId, 99.9m, "BRL"), CancellationToken.None);

        output.Sku.Should().Be("SKU-1");
        var stored = await products.GetBySkuAsync("SKU-1", CancellationToken.None);
        stored!.Slug.Should().Be(Slug.Create("wireless-mouse"));
    }

    [Fact]
    public async Task ExecuteAsync_rejects_an_unknown_category()
    {
        var products = new FakeProductRepository();
        var useCase = new CreateProductUseCase(products, new FakeCategoryRepository(), new FakeAuditLogService(), TimeProvider.System);

        var act = () => useCase.ExecuteAsync(
            new CreateProductCommand("SKU-1", "Wireless Mouse", Guid.NewGuid(), 99.9m, "BRL"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_duplicate_sku()
    {
        var (products, categories, categoryId) = await SetupAsync();
        var useCase = new CreateProductUseCase(products, categories, new FakeAuditLogService(), TimeProvider.System);
        await useCase.ExecuteAsync(new CreateProductCommand("SKU-1", "Wireless Mouse", categoryId, 99.9m, "BRL"), CancellationToken.None);

        var act = () => useCase.ExecuteAsync(
            new CreateProductCommand("SKU-1", "Another Mouse", categoryId, 50m, "BRL"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
