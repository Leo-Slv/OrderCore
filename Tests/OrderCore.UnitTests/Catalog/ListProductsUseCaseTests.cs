using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

public sealed class ListProductsUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static async Task<Product> AddProductAsync(FakeProductRepository products, string sku)
    {
        var product = Product.Create(sku, $"Product {sku}", Slug.GenerateFrom(sku), Guid.NewGuid(), 10m, "BRL", Now);
        await products.AddAsync(product, CancellationToken.None);
        return product;
    }

    [Fact]
    public async Task ExecuteAsync_returns_the_page_with_totals_and_each_products_availability()
    {
        var products = new FakeProductRepository();
        var availability = new FakeStockAvailabilityProvider();
        var first = await AddProductAsync(products, "SKU-1");
        var second = await AddProductAsync(products, "SKU-2");
        await AddProductAsync(products, "SKU-3");
        availability.Set(second.Id, StockAvailability.OutOfStock);

        var result = await new ListProductsUseCase(products, availability)
            .ExecuteAsync(new ListProductsFilter { Page = 1, PageSize = 2 }, includeUnpublished: true, CancellationToken.None);

        result.TotalItems.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Single(i => i.Id == first.Id).Availability.Should().Be(StockAvailability.InStock);
        result.Items.Single(i => i.Id == second.Id).Availability.Should().Be(StockAvailability.OutOfStock);
    }

    [Fact]
    public async Task ExecuteAsync_picks_the_primary_image_even_when_it_is_not_first()
    {
        var products = new FakeProductRepository();
        var product = await AddProductAsync(products, "SKU-1");
        product.AddImage("https://img/secondary.png", null, isPrimary: false, Now);
        product.AddImage("https://img/primary.png", null, isPrimary: true, Now);

        var result = await new ListProductsUseCase(products, new FakeStockAvailabilityProvider())
            .ExecuteAsync(new ListProductsFilter(), includeUnpublished: true, CancellationToken.None);

        result.Items.Single().PrimaryImageUrl.Should().Be("https://img/primary.png");
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, ListProductsFilter.MaximumPageSize + 1)]
    public async Task ExecuteAsync_rejects_out_of_range_paging(int page, int pageSize)
    {
        var useCase = new ListProductsUseCase(new FakeProductRepository(), new FakeStockAvailabilityProvider());

        var act = () => useCase.ExecuteAsync(
            new ListProductsFilter { Page = page, PageSize = pageSize }, includeUnpublished: true, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ExecuteAsync_for_the_public_lists_only_published_products()
    {
        var products = new FakeProductRepository();
        var published = await AddProductAsync(products, "SKU-1");
        published.Publish(Now);
        await AddProductAsync(products, "SKU-2");

        var result = await new ListProductsUseCase(products, new FakeStockAvailabilityProvider())
            .ExecuteAsync(new ListProductsFilter(), includeUnpublished: false, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Id.Should().Be(published.Id);
        result.TotalItems.Should().Be(1);
    }
}
