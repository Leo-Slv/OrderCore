using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

public sealed class GetProductBySlugUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static async Task<(FakeProductRepository Products, Product Product)> SetupAsync()
    {
        var products = new FakeProductRepository();
        var product = Product.Create("SKU-1", "Wireless Mouse", Slug.Create("wireless-mouse"), Guid.NewGuid(), 99.9m, "BRL", Now);
        await products.AddAsync(product, CancellationToken.None);
        return (products, product);
    }

    [Fact]
    public async Task ExecuteAsync_returns_a_published_product_with_its_availability()
    {
        var (products, product) = await SetupAsync();
        product.Publish(Now);
        var availability = new FakeStockAvailabilityProvider();
        availability.Set(product.Id, StockAvailability.LowStock);

        var output = await new GetProductBySlugUseCase(products, availability).ExecuteAsync("wireless-mouse", CancellationToken.None);

        output.Id.Should().Be(product.Id);
        output.Availability.Should().Be(StockAvailability.LowStock);
    }

    [Fact]
    public async Task ExecuteAsync_hides_a_draft_product()
    {
        var (products, _) = await SetupAsync();
        var useCase = new GetProductBySlugUseCase(products, new FakeStockAvailabilityProvider());

        var act = () => useCase.ExecuteAsync("wireless-mouse", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "product_not_found");
    }

    [Fact]
    public async Task ExecuteAsync_hides_a_discontinued_product()
    {
        var (products, product) = await SetupAsync();
        product.Publish(Now);
        product.Discontinue();
        var useCase = new GetProductBySlugUseCase(products, new FakeStockAvailabilityProvider());

        var act = () => useCase.ExecuteAsync("wireless-mouse", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("unknown-product")]
    [InlineData("Not A Slug!")]
    [InlineData("")]
    public async Task ExecuteAsync_reports_unknown_or_malformed_slugs_as_not_found(string slug)
    {
        var (products, _) = await SetupAsync();
        var useCase = new GetProductBySlugUseCase(products, new FakeStockAvailabilityProvider());

        var act = () => useCase.ExecuteAsync(slug, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "product_not_found");
    }
}
