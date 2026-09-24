using FluentAssertions;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Orders;

public sealed class CheckoutUseCaseTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeProductCatalog _catalog = new();
    private readonly FakeCustomerDirectory _customers = new();
    private readonly FakeInventoryService _inventory = new();
    private readonly FakePaymentGateway _payments = new();
    private readonly Guid _addressId;

    public CheckoutUseCaseTests()
    {
        _addressId = _customers.AddAddress(CustomerId);
    }

    private CheckoutUseCase CreateUseCase() => new(
        _orders, _catalog, _customers, _inventory, _payments, new FakeOrderNumberGenerator(), new FakeAuditLogService(), TimeProvider.System);

    private CatalogProductSnapshot InStockProduct(decimal price = 50m, int stock = 10, string currency = "BRL")
    {
        var product = _catalog.Add(price, currency);
        _inventory.SetAvailable(product.Id, stock);
        return product;
    }

    private CheckoutCommand Command(
        IReadOnlyList<CheckoutItem> items,
        string idempotencyKey = "checkout-1",
        PaymentMethodChoice method = PaymentMethodChoice.Pix,
        decimal? expectedTotal = null) =>
        new(CustomerId, items, _addressId, _addressId, method, idempotencyKey, CustomerNotes: "leave at the door", expectedTotal);

    [Fact]
    public async Task ExecuteAsync_creates_a_pending_payment_order_reserves_stock_and_requests_payment()
    {
        var product = InStockProduct(price: 50m);

        var orderId = await CreateUseCase().ExecuteAsync(Command([new CheckoutItem(product.Id, 2)]), CancellationToken.None);

        var order = _orders.Orders.Should().ContainSingle().Subject;
        order.Id.Should().Be(orderId);
        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.TotalAmount.Should().Be(100m);
        order.ShippingAddress!.Street.Should().Be("Main St");
        order.CustomerNotes.Should().Be("leave at the door");
        order.CheckoutIdempotencyKey.Should().Be("checkout-1");
        _inventory.Reserved.Should().ContainKey(orderId);
        _payments.Requests.Should().ContainSingle().Which.Should().Be((orderId, 100m, PaymentMethodChoice.Pix));
    }

    [Fact]
    public async Task ExecuteAsync_replayed_with_the_same_key_returns_the_same_order_without_a_second_payment()
    {
        var product = InStockProduct();
        var useCase = CreateUseCase();

        var first = await useCase.ExecuteAsync(Command([new CheckoutItem(product.Id, 1)]), CancellationToken.None);
        var replay = await useCase.ExecuteAsync(Command([new CheckoutItem(product.Id, 1)]), CancellationToken.None);

        replay.Should().Be(first);
        _orders.Orders.Should().ContainSingle();
        _payments.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_replayed_after_payment_failed_to_start_requests_payment_again()
    {
        var product = InStockProduct();
        var useCase = CreateUseCase();
        _payments.FailNextRequestWith = new TimeoutException("provider timeout");

        var firstAttempt = () => useCase.ExecuteAsync(Command([new CheckoutItem(product.Id, 1)]), CancellationToken.None);
        await firstAttempt.Should().ThrowAsync<TimeoutException>();

        var orderId = await useCase.ExecuteAsync(Command([new CheckoutItem(product.Id, 1)]), CancellationToken.None);

        _orders.Orders.Should().ContainSingle().Which.Id.Should().Be(orderId);
        _payments.Requests.Should().ContainSingle().Which.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task ExecuteAsync_without_enough_stock_persists_nothing()
    {
        var product = InStockProduct(stock: 1);

        var act = () => CreateUseCase().ExecuteAsync(Command([new CheckoutItem(product.Id, 2)]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "insufficient_stock");
        _orders.Orders.Should().BeEmpty();
        _inventory.Reserved.Should().BeEmpty();
        _payments.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_releases_the_reservation_when_saving_the_order_fails()
    {
        var product = InStockProduct();
        _orders.FailNextSaveWith = new InvalidOperationException("database down");

        var act = () => CreateUseCase().ExecuteAsync(Command([new CheckoutItem(product.Id, 1)]), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _inventory.Reserved.Should().BeEmpty();
        _payments.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_losing_a_race_on_the_same_key_returns_the_winners_order()
    {
        var product = InStockProduct();
        var winner = Order.Create(CustomerId, "BRL", "ORD-2026-999999", DateTimeOffset.UtcNow, checkoutIdempotencyKey: "checkout-1");
        _orders.FailNextSaveWith = new InvalidOperationException("duplicate key");
        _orders.OnFailingSave = () => _orders.Store(winner);

        var orderId = await CreateUseCase().ExecuteAsync(Command([new CheckoutItem(product.Id, 1)]), CancellationToken.None);

        orderId.Should().Be(winner.Id);
        _inventory.Reserved.Should().BeEmpty();
        _payments.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_total_that_changed_since_the_buyer_saw_it()
    {
        var product = InStockProduct(price: 50m);

        var act = () => CreateUseCase().ExecuteAsync(
            Command([new CheckoutItem(product.Id, 2)], expectedTotal: 90m), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "price_changed");
        _orders.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_accepts_a_matching_expected_total()
    {
        var product = InStockProduct(price: 50m);

        await CreateUseCase().ExecuteAsync(Command([new CheckoutItem(product.Id, 2)], expectedTotal: 100m), CancellationToken.None);

        _orders.Orders.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_product_that_is_no_longer_purchasable()
    {
        var product = _catalog.Add(50m, isPurchasable: false);
        _inventory.SetAvailable(product.Id, 10);

        var act = () => CreateUseCase().ExecuteAsync(Command([new CheckoutItem(product.Id, 1)]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "product_unavailable");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_an_unknown_product()
    {
        var act = () => CreateUseCase().ExecuteAsync(Command([new CheckoutItem(Guid.NewGuid(), 1)]), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "product_not_found");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_products_in_different_currencies()
    {
        var real = InStockProduct(currency: "BRL");
        var dollar = InStockProduct(currency: "USD");

        var act = () => CreateUseCase().ExecuteAsync(
            Command([new CheckoutItem(real.Id, 1), new CheckoutItem(dollar.Id, 1)]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "mixed_currencies");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_an_address_that_is_not_the_customers()
    {
        var product = InStockProduct();
        var command = Command([new CheckoutItem(product.Id, 1)]) with { ShippingAddressId = Guid.NewGuid() };

        var act = () => CreateUseCase().ExecuteAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "address_not_found");
        _inventory.Reserved.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_an_empty_cart()
    {
        var act = () => CreateUseCase().ExecuteAsync(Command([]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "order_without_items");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_rejects_a_non_positive_quantity(int quantity)
    {
        var product = InStockProduct();

        var act = () => CreateUseCase().ExecuteAsync(Command([new CheckoutItem(product.Id, quantity)]), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ExecuteAsync_requires_an_idempotency_key()
    {
        var product = InStockProduct();

        var act = () => CreateUseCase().ExecuteAsync(
            Command([new CheckoutItem(product.Id, 1)], idempotencyKey: " "), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
