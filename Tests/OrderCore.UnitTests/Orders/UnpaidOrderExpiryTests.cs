using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>
/// An order waiting for payment with no payment started stops holding stock
/// after 30 minutes (Docs/specs/orders/unpaid-order-expiry.md), and an
/// authorization that arrives for an order that already ended is voided.
/// </summary>
public sealed class UnpaidOrderExpiryTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeOrderRepository _orders = new();
    private readonly FakeInventoryService _inventory = new();
    private readonly FakePaymentGateway _payments = new();
    private readonly FakeAuditLogService _auditLog = new();

    private ExpireUnpaidOrderUseCase UseCase() =>
        new(
            _orders,
            _payments,
            new MarkOrderPaymentFailedUseCase(
                _orders, _inventory, _auditLog, TestMetrics.Orders, _clock, NullLogger<MarkOrderPaymentFailedUseCase>.Instance),
            TestMetrics.Orders,
            _clock);

    /// <summary>An order that reserved its stock and waits for payment; <paramref name="withPayment"/> starts one.</summary>
    private async Task<Order> WaitingOrderAsync(bool withPayment = false)
    {
        var order = Order.Create(Guid.NewGuid(), "BRL", $"ORD-{Guid.NewGuid():N}"[..12], _clock.GetUtcNow());
        var productId = Guid.NewGuid();
        order.AddItem(productId, productVariantId: null, "SKU-1", "Widget", productImageUrl: null, unitPrice: 10m, quantity: 2);
        _inventory.SetAvailable(productId, 2);
        await _inventory.TryReserveOrderItemsAsync(order, CancellationToken.None);
        order.RequestPayment(_clock.GetUtcNow());
        if (withPayment)
        {
            await _payments.RequestPaymentAsync(order.Id, order.TotalAmount, "BRL", PaymentMethodChoice.Card, order.Id.ToString(), CancellationToken.None);
        }

        order.ClearDomainEvents();
        _orders.Store(order);
        return order;
    }

    private async Task<int> RunAsync()
    {
        var useCase = UseCase();
        var ended = 0;
        foreach (var id in await useCase.FindExpiredAsync(Window, 100, CancellationToken.None))
        {
            ended += await useCase.ExpireAsync(id, Window, CancellationToken.None) ? 1 : 0;
        }

        return ended;
    }

    [Fact]
    public async Task An_order_without_a_payment_ends_after_30_minutes_and_releases_its_stock()
    {
        var order = await WaitingOrderAsync();
        order.PaymentRequestedAt.Should().Be(_clock.GetUtcNow());

        _clock.Advance(TimeSpan.FromMinutes(29));
        (await RunAsync()).Should().Be(0, "the customer can still repeat the checkout");

        _clock.Advance(TimeSpan.FromMinutes(2));
        (await RunAsync()).Should().Be(1);

        order.Status.Should().Be(OrderStatus.PaymentFailed);
        _inventory.Reserved.Should().NotContainKey(order.Id);
        _auditLog.Entries.Should().Contain(e => e.Action == "OrderPaymentFailed" && e.Metadata!["reason"] == "payment_not_started");
        (await RunAsync()).Should().Be(0, "an order that ended isn't ended twice");
    }

    [Fact]
    public async Task An_order_with_a_payment_is_left_to_the_payment_window()
    {
        var order = await WaitingOrderAsync(withPayment: true);

        _clock.Advance(TimeSpan.FromHours(1));
        (await RunAsync()).Should().Be(0);

        order.Status.Should().Be(OrderStatus.PendingPayment);
        _inventory.Reserved.Should().ContainKey(order.Id);
    }

    [Fact]
    public async Task A_late_authorization_for_an_order_that_ended_is_voided()
    {
        var order = await WaitingOrderAsync();
        _clock.Advance(TimeSpan.FromMinutes(31));
        await RunAsync();
        var confirm = new ConfirmOrderUseCase(
            _orders, _inventory, _payments, _auditLog, TestMetrics.Orders, _clock, NullLogger<ConfirmOrderUseCase>.Instance);

        await confirm.ExecuteAsync(order.Id, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.PaymentFailed);
        _payments.Settlements.Should().ContainSingle().Which.Should().Be((order.Id, "order_no_longer_waiting"));
    }

    [Fact]
    public async Task A_late_authorization_for_a_confirmed_or_shipped_order_settles_nothing()
    {
        var order = await WaitingOrderAsync(withPayment: true);
        order.Confirm(_clock.GetUtcNow());
        var confirm = new ConfirmOrderUseCase(
            _orders, _inventory, _payments, _auditLog, TestMetrics.Orders, _clock, NullLogger<ConfirmOrderUseCase>.Instance);

        await confirm.ExecuteAsync(order.Id, CancellationToken.None);

        _payments.Settlements.Should().BeEmpty();
    }
}
