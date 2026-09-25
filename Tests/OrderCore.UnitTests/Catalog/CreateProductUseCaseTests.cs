using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
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

    private static CreateProductUseCase CreateUseCase(
        FakeProductRepository products, FakeCategoryRepository categories, FakeStockAvailabilityProvider? stock = null)
    {
        stock ??= new FakeStockAvailabilityProvider();
        return new(products, stock, stock, categories, new FakeAuditLogService(), TimeProvider.System);
    }

    [Fact]
    public async Task ExecuteAsync_creates_a_product_with_a_slug_derived_from_the_name()
    {
        var (products, categories, categoryId) = await SetupAsync();
        var useCase = CreateUseCase(products, categories);

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
        var useCase = CreateUseCase(products, new FakeCategoryRepository());

        var act = () => useCase.ExecuteAsync(
            new CreateProductCommand("SKU-1", "Wireless Mouse", Guid.NewGuid(), 99.9m, "BRL"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "category_not_found");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_duplicate_sku()
    {
        var (products, categories, categoryId) = await SetupAsync();
        var useCase = CreateUseCase(products, categories);
        await useCase.ExecuteAsync(new CreateProductCommand("SKU-1", "Wireless Mouse", categoryId, 99.9m, "BRL"), CancellationToken.None);

        var act = () => useCase.ExecuteAsync(
            new CreateProductCommand("SKU-1", "Another Mouse", categoryId, 50m, "BRL"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "sku_already_exists");
    }

    [Fact]
    public async Task ExecuteAsync_appends_the_sku_when_another_product_already_has_the_name_slug()
    {
        var (products, categories, categoryId) = await SetupAsync();
        var useCase = CreateUseCase(products, categories);
        await useCase.ExecuteAsync(new CreateProductCommand("SKU-1", "Wireless Mouse", categoryId, 99.9m, "BRL"), CancellationToken.None);

        var output = await useCase.ExecuteAsync(
            new CreateProductCommand("SKU-2", "Wireless Mouse", categoryId, 79.9m, "BRL"), CancellationToken.None);

        output.Slug.Should().Be("wireless-mouse-sku-2");
    }

    [Fact]
    public async Task ExecuteAsync_returns_the_product_with_its_availability()
    {
        var (products, categories, categoryId) = await SetupAsync();

        var output = await CreateUseCase(products, categories).ExecuteAsync(
            new CreateProductCommand("SKU-1", "Wireless Mouse", categoryId, 99.9m, "BRL"), CancellationToken.None);

        output.Availability.Should().Be(StockAvailability.InStock);
        output.Currency.Should().Be("BRL");
    }
}
