using FluentAssertions;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
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

    [Fact]
    public void Create_sets_initial_status_to_Created()
    {
        var order = Order.Create(CustomerId, "BRL", DateTimeOffset.UtcNow);

        order.Status.Should().Be(OrderStatus.Created);
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_accumulates_quantity_for_the_same_product()
    {
        var order = Order.Create(CustomerId, "BRL", DateTimeOffset.UtcNow);

        order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 1);
        order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 2);

        order.Items.Should().ContainSingle();
        order.Items.Single().Quantity.Should().Be(3);
        order.TotalAmount.Should().Be(30m);
    }

    [Fact]
    public void AddItem_after_payment_requested_is_rejected()
    {
        var order = Order.Create(CustomerId, "BRL", DateTimeOffset.UtcNow);
        order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 1);
        order.RequestPayment(DateTimeOffset.UtcNow);

        var act = () => order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Full_happy_path_moves_through_every_expected_state()
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(CustomerId, "BRL", now);
        order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 1);

        order.RequestPayment(now);
        order.Status.Should().Be(OrderStatus.PendingPayment);

        order.Confirm(now);
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.StartProcessing(now);
        order.Status.Should().Be(OrderStatus.Processing);

        order.Ship(now);
        order.Status.Should().Be(OrderStatus.Shipped);

        order.Deliver(now);
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Delivered_order_cannot_transition_back_to_PendingPayment()
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(CustomerId, "BRL", now);
        order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 1);
        order.RequestPayment(now);
        order.Confirm(now);
        order.StartProcessing(now);
        order.Ship(now);
        order.Deliver(now);

        var act = () => order.RequestPayment(now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_is_rejected_once_order_has_shipped()
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(CustomerId, "BRL", now);
        order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 1);
        order.RequestPayment(now);
        order.Confirm(now);
        order.StartProcessing(now);
        order.Ship(now);

        var act = () => order.Cancel("customer request", now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FailPayment_from_PendingPayment_raises_domain_event()
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(CustomerId, "BRL", now);
        order.AddItem(ProductId, "Widget", unitPrice: 10m, quantity: 1);
        order.RequestPayment(now);
        order.ClearDomainEvents();

        order.FailPayment("card_declined", now);

        order.Status.Should().Be(OrderStatus.PaymentFailed);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCore.Api.Modules.Orders.Domain.Events.OrderPaymentFailed);
    }
}
