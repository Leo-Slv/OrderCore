using FluentAssertions;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Infrastructure.Messaging;
using OrderCore.Api.Shared.Application.Messaging;
using Xunit;
using IntegrationEvents = OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

namespace OrderCore.UnitTests.Orders;

/// <summary>Each order transition is announced by exactly its own contract, carrying the order's summary.</summary>
public sealed class OrderIntegrationEventTranslatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Order NewOrder()
    {
        var order = Order.Create(CustomerId, "BRL", "ORD-2026-000042", Now);
        order.AddItem(Guid.NewGuid(), productVariantId: null, "SKU-1", "Widget", productImageUrl: null, unitPrice: 25m, quantity: 2);
        return order;
    }

    private static IReadOnlyList<IntegrationEvent> Translated(Order order) =>
        order.DomainEvents
            .Select(e => OrderIntegrationEventTranslator.Translate(order, e))
            .OfType<IntegrationEvent>()
            .ToList();

    /// <summary>What <paramref name="transition"/> announces, leaving out what was raised before it.</summary>
    private static IReadOnlyList<IntegrationEvent> Announced(Order order, Action<Order> transition)
    {
        order.ClearDomainEvents();
        transition(order);
        return Translated(order);
    }

    [Fact]
    public void Creating_an_order_announces_order_created_with_its_summary()
    {
        var order = NewOrder();

        var created = Translated(order).Should().ContainSingle().Which.Should().BeOfType<IntegrationEvents.OrderCreated>().Subject;

        created.OrderId.Should().Be(order.Id);
        created.OrderNumber.Should().Be("ORD-2026-000042");
        created.CustomerId.Should().Be(CustomerId);
        created.Status.Should().Be("Created");
        created.TotalAmount.Should().Be(50m);
        created.Currency.Should().Be("BRL");
        created.Version.Should().Be(1);
        created.EventId.Should().Be(order.DomainEvents.Single().EventId, "the contract keeps the domain event's id");
    }

    [Fact]
    public void Every_transition_announces_exactly_its_own_event_and_status()
    {
        var order = NewOrder();

        Announced(order, o => o.RequestPayment(Now)).Should().ContainSingle()
            .Which.Should().BeOfType<IntegrationEvents.OrderPaymentRequested>().Which.Status.Should().Be("PendingPayment");
        Announced(order, o => o.Confirm(Now)).Should().ContainSingle()
            .Which.Should().BeOfType<IntegrationEvents.OrderConfirmed>().Which.Status.Should().Be("Confirmed");
        Announced(order, o => o.StartProcessing(Now)).Should().ContainSingle()
            .Which.Should().BeOfType<IntegrationEvents.OrderProcessingStarted>().Which.Status.Should().Be("Processing");
        Announced(order, o => o.Ship(Now)).Should().ContainSingle()
            .Which.Should().BeOfType<IntegrationEvents.OrderShipped>().Which.Status.Should().Be("Shipped");
        Announced(order, o => o.Deliver(Now)).Should().ContainSingle()
            .Which.Should().BeOfType<IntegrationEvents.OrderDelivered>().Which.Status.Should().Be("Delivered");
    }

    [Fact]
    public void A_failed_payment_announces_the_reason()
    {
        var order = NewOrder();
        order.RequestPayment(Now);

        var failed = Announced(order, o => o.FailPayment("card_declined", Now)).Should().ContainSingle()
            .Which.Should().BeOfType<IntegrationEvents.OrderPaymentFailed>().Subject;

        failed.Status.Should().Be("PaymentFailed");
        failed.Reason.Should().Be("card_declined");
    }

    [Fact]
    public void A_cancellation_announces_the_reason()
    {
        var order = NewOrder();

        var cancelled = Announced(order, o => o.Cancel("changed my mind", Now)).Should().ContainSingle()
            .Which.Should().BeOfType<IntegrationEvents.OrderCancelled>().Subject;

        cancelled.Status.Should().Be("Cancelled");
        cancelled.Reason.Should().Be("changed my mind");
    }
}
