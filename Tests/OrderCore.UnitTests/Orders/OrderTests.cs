using FluentAssertions;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>
/// Unit tests live under a folder per business module (Orders, Customers,
/// Catalog, Inventory, Payments) rather than per technical layer, mirroring
/// src/ and easing the future extraction of Payments into PayCore
/// (section 5 / section 33 of the project context).
/// </summary>
public sealed class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static Order CreateOrder(DateTimeOffset now) => Order.Create(CustomerId, "BRL", "ORD-2026-000001", now);

    private static void AddWidget(Order order, int quantity = 1) =>
        order.AddItem(ProductId, productVariantId: null, "SKU-1", "Widget", productImageUrl: null, unitPrice: 10m, quantity);

    [Fact]
    public void Create_sets_initial_status_to_Created()
    {
        var order = CreateOrder(DateTimeOffset.UtcNow);

        order.Status.Should().Be(OrderStatus.Created);
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_accumulates_quantity_for_the_same_product()
    {
        var order = CreateOrder(DateTimeOffset.UtcNow);

        AddWidget(order, 1);
        AddWidget(order, 2);

        order.Items.Should().ContainSingle();
        order.Items.Single().Quantity.Should().Be(3);
        order.TotalAmount.Should().Be(30m);
    }

    [Fact]
    public void AddItem_after_payment_requested_is_rejected()
    {
        var order = CreateOrder(DateTimeOffset.UtcNow);
        AddWidget(order);
        order.RequestPayment(DateTimeOffset.UtcNow);

        var act = () => AddWidget(order);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Full_happy_path_moves_through_every_expected_state()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        AddWidget(order);

        order.RequestPayment(now);
        order.Status.Should().Be(OrderStatus.PendingPayment);

        order.Confirm(now);
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.StartProcessing(now);
        order.Status.Should().Be(OrderStatus.Processing);

        order.Ship(now);
        order.Status.Should().Be(OrderStatus.Shipped);
        order.ShippedAt.Should().Be(now);

        order.Deliver(now);
        order.Status.Should().Be(OrderStatus.Delivered);
        order.DeliveredAt.Should().Be(now);
    }

    [Fact]
    public void Delivered_order_cannot_transition_back_to_PendingPayment()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        AddWidget(order);
        order.RequestPayment(now);
        order.Confirm(now);
        order.StartProcessing(now);
        order.Ship(now);
        order.Deliver(now);

        var act = () => order.RequestPayment(now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Cancel_is_rejected_once_order_has_shipped()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        AddWidget(order);
        order.RequestPayment(now);
        order.Confirm(now);
        order.StartProcessing(now);
        order.Ship(now);

        var act = () => order.Cancel("customer request", now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void FailPayment_from_PendingPayment_raises_domain_event()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        AddWidget(order);
        order.RequestPayment(now);
        order.ClearDomainEvents();

        order.FailPayment("card_declined", now);

        order.Status.Should().Be(OrderStatus.PaymentFailed);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCore.Api.Modules.Orders.Domain.Events.OrderPaymentFailed);
    }

    [Fact]
    public void ApplyDiscount_rejects_more_than_the_subtotal()
    {
        var order = CreateOrder(DateTimeOffset.UtcNow);
        AddWidget(order);

        var act = () => order.ApplyDiscount(11m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TotalAmount_reflects_discount_shipping_and_tax()
    {
        var order = CreateOrder(DateTimeOffset.UtcNow);
        AddWidget(order);

        order.ApplyDiscount(2m);
        order.SetShippingAmount(5m);
        order.SetTaxAmount(1m);

        order.TotalAmount.Should().Be(10m - 2m + 5m + 1m);
    }

    [Fact]
    public void SetInternalNotes_updates_the_note()
    {
        var order = CreateOrder(DateTimeOffset.UtcNow);

        order.SetInternalNotes("fragile, handle with care");

        order.InternalNotes.Should().Be("fragile, handle with care");
    }
}
