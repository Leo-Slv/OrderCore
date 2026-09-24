using FluentAssertions;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Orders;

public sealed class QuoteCartUseCaseTests
{
    private readonly FakeProductCatalog _catalog = new();
    private readonly FakeInventoryService _inventory = new();

    private QuoteCartUseCase CreateUseCase() => new(_catalog, _inventory);

    private CatalogProductSnapshot Product(decimal price, int stock = 10, bool isPurchasable = true, string currency = "BRL")
    {
        var product = _catalog.Add(price, currency, isPurchasable);
        _inventory.SetAvailable(product.Id, stock);
        return product;
    }

    [Fact]
    public async Task ExecuteAsync_prices_a_clean_cart_at_current_prices()
    {
        var mouse = Product(150m);
        var pad = Product(80m);

        var quote = await CreateUseCase().ExecuteAsync(
            [new QuoteCartLine(mouse.Id, 2, 150m), new QuoteCartLine(pad.Id, 1, null)], CancellationToken.None);

        quote.IsValid.Should().BeTrue();
        quote.Currency.Should().Be("BRL");
        quote.Total.Should().Be(380m);
        quote.Lines.Should().OnlyContain(l => l.Issue == null);
        quote.Lines.Single(l => l.ProductId == mouse.Id).LineTotal.Should().Be(300m);
    }

    [Fact]
    public async Task ExecuteAsync_flags_a_price_change_and_still_counts_the_line_at_the_new_price()
    {
        var mouse = Product(170m);

        var quote = await CreateUseCase().ExecuteAsync([new QuoteCartLine(mouse.Id, 1, 150m)], CancellationToken.None);

        quote.IsValid.Should().BeFalse();
        quote.Total.Should().Be(170m);
        var line = quote.Lines.Single();
        line.Issue.Should().Be(CartLineIssue.PriceChanged);
        line.UnitPrice.Should().Be(170m);
        line.PreviousUnitPrice.Should().Be(150m);
    }

    [Fact]
    public async Task ExecuteAsync_excludes_lines_that_cannot_be_bought_from_the_total()
    {
        var available = Product(100m);
        var lowStock = Product(40m, stock: 1);
        var discontinued = Product(30m, isPurchasable: false);
        var missing = Guid.NewGuid();

        var quote = await CreateUseCase().ExecuteAsync(
            [
                new QuoteCartLine(available.Id, 1, null),
                new QuoteCartLine(lowStock.Id, 2, null),
                new QuoteCartLine(discontinued.Id, 1, null),
                new QuoteCartLine(missing, 1, null),
            ],
            CancellationToken.None);

        quote.IsValid.Should().BeFalse();
        quote.Total.Should().Be(100m);
        quote.Lines.Single(l => l.ProductId == lowStock.Id).Issue.Should().Be(CartLineIssue.InsufficientStock);
        quote.Lines.Single(l => l.ProductId == discontinued.Id).Issue.Should().Be(CartLineIssue.Unavailable);
        var notFound = quote.Lines.Single(l => l.ProductId == missing);
        notFound.Issue.Should().Be(CartLineIssue.NotFound);
        notFound.ProductName.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_reports_only_the_most_serious_issue()
    {
        var discontinuedAndRepriced = Product(30m, stock: 0, isPurchasable: false);

        var quote = await CreateUseCase().ExecuteAsync(
            [new QuoteCartLine(discontinuedAndRepriced.Id, 1, 25m)], CancellationToken.None);

        quote.Lines.Single().Issue.Should().Be(CartLineIssue.Unavailable);
        quote.Lines.Single().PreviousUnitPrice.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_for_an_empty_cart_is_not_valid()
    {
        var quote = await CreateUseCase().ExecuteAsync([], CancellationToken.None);

        quote.IsValid.Should().BeFalse();
        quote.Total.Should().Be(0m);
        quote.Currency.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_mixed_currencies()
    {
        var real = Product(10m, currency: "BRL");
        var dollar = Product(10m, currency: "USD");

        var act = () => CreateUseCase().ExecuteAsync(
            [new QuoteCartLine(real.Id, 1, null), new QuoteCartLine(dollar.Id, 1, null)], CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "mixed_currencies");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_product_listed_twice()
    {
        var mouse = Product(10m);

        var act = () => CreateUseCase().ExecuteAsync(
            [new QuoteCartLine(mouse.Id, 1, null), new QuoteCartLine(mouse.Id, 2, null)], CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_non_positive_quantity()
    {
        var mouse = Product(10m);

        var act = () => CreateUseCase().ExecuteAsync([new QuoteCartLine(mouse.Id, 0, null)], CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
