using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

public sealed class PublishProductUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_publishes_a_draft_product()
    {
        var products = new FakeProductRepository();
        var product = Product.Create("SKU-1", "Widget", Slug.Create("widget"), Guid.NewGuid(), 10m, "BRL", DateTimeOffset.UtcNow);
        await products.AddAsync(product, CancellationToken.None);
        var useCase = new PublishProductUseCase(products, new FakeAuditLogService(), TimeProvider.System);

        var output = await useCase.ExecuteAsync(product.Id, CancellationToken.None);

        output.Status.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public async Task ExecuteAsync_for_unknown_product_throws()
    {
        var useCase = new PublishProductUseCase(new FakeProductRepository(), new FakeAuditLogService(), TimeProvider.System);

        var act = () => useCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "product_not_found");
    }
}
