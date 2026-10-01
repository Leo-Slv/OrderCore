using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.Telemetry;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.UnitTests.AuditLogs;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>Each order event is counted once, with the tags the dashboards group by.</summary>
public sealed class OrdersMetricsTests : IDisposable
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly MetricsProbe _probe = new();
    private readonly OrdersMetrics _metrics;
    private readonly FakeOrderRepository _orders = new();
    private readonly FakeProductCatalog _catalog = new();
    private readonly FakeCustomerDirectory _customers = new();
    private readonly FakeInventoryService _inventory = new();
    private readonly FakePaymentGateway _payments = new();
    private readonly FakeAuditLogService _auditLog = new();
    private readonly Guid _addressId;

    public OrdersMetricsTests()
    {
        _metrics = new OrdersMetrics(_probe.Factory);
        _addressId = _customers.AddAddress(CustomerId);
    }

    public void Dispose() => _probe.Dispose();

    private CheckoutUseCase Checkout() => new(
        _orders, _catalog, _customers, _inventory, _payments, new FakeOrderNumberGenerator(), _auditLog, _metrics, TimeProvider.System, new FakeCurrentUser());

    private CheckoutCommand Command(Guid productId, int quantity = 1, string key = "key-1") =>
        new(CustomerId, [new CheckoutItem(productId, quantity)], _addressId, _addressId, PaymentMethodChoice.Pix, key, null, null);

    private Guid ProductWithStock(int stock, decimal price = 40m)
    {
        var product = _catalog.Add(price, "BRL");
        _inventory.SetAvailable(product.Id, stock);
        return product.Id;
    }

    [Fact]
    public async Task A_placed_checkout_counts_one_order_and_its_duration()
    {
        var productId = ProductWithStock(5);

        await Checkout().ExecuteAsync(Command(productId), CancellationToken.None);

        _probe.Of("ordercore.orders.created").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.channel", "checkout");
        _probe.Of("ordercore.checkout.duration").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.outcome", "placed");
        _probe.Of("ordercore.checkout.refusals").Should().BeEmpty();
    }

    [Fact]
    public async Task Repeating_a_checkout_does_not_count_a_second_order()
    {
        var productId = ProductWithStock(5);
        var checkout = Checkout();

        await checkout.ExecuteAsync(Command(productId), CancellationToken.None);
        await checkout.ExecuteAsync(Command(productId), CancellationToken.None);

        _probe.Of("ordercore.orders.created").Should().ContainSingle();
        _probe.Of("ordercore.checkout.duration").Select(m => m.Tags["ordercore.outcome"]).Should().Equal("placed", "repeated");
    }

    [Fact]
    public async Task A_refused_checkout_counts_its_error_code()
    {
        var productId = ProductWithStock(1);

        var act = () => Checkout().ExecuteAsync(Command(productId, quantity: 3), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _probe.Of("ordercore.checkout.refusals").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.error_code", "insufficient_stock");
        _probe.Of("ordercore.checkout.duration").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.outcome", "refused");
        _probe.Of("ordercore.orders.created").Should().BeEmpty();
    }

    [Fact]
    public async Task A_confirmed_order_counts_once_with_its_value_per_currency()
    {
        var productId = ProductWithStock(5, price: 40m);
        var orderId = (await Checkout().ExecuteAsync(Command(productId, quantity: 2), CancellationToken.None)).OrderId;
        var confirm = new ConfirmOrderUseCase(
            _orders, _inventory, _payments, _auditLog, _metrics, TimeProvider.System, NullLogger<ConfirmOrderUseCase>.Instance);

        await confirm.ExecuteAsync(orderId, CancellationToken.None);
        await confirm.ExecuteAsync(orderId, CancellationToken.None);

        _probe.Of("ordercore.orders.confirmed").Should().ContainSingle("a second confirmation changes nothing")
            .Which.Tags.Should().Contain("ordercore.currency", "BRL");
        _probe.Of("ordercore.orders.confirmed_value").Should().ContainSingle().Which.Value.Should().Be(80);
    }

    [Theory]
    [InlineData(true, "customer")]
    [InlineData(false, "admin")]
    public async Task A_cancellation_counts_who_cancelled(bool byTheCustomer, string cancelledBy)
    {
        var productId = ProductWithStock(5);
        var orderId = (await Checkout().ExecuteAsync(Command(productId), CancellationToken.None)).OrderId;
        var cancel = new CancelOrderUseCase(_orders, _inventory, _payments, _auditLog, _metrics, TimeProvider.System);

        await cancel.ExecuteAsync(new CancelOrderCommand(orderId, "changed my mind", byTheCustomer ? CustomerId : null), CancellationToken.None);

        _probe.Of("ordercore.orders.cancelled").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.cancelled_by", cancelledBy);
    }
}
