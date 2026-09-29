using FluentAssertions;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>An order event becomes the light update the screens get.</summary>
public sealed class OrderUpdatesBroadcasterTests
{
    private sealed class RecordingNotifier : IOrderUpdatesNotifier
    {
        public List<OrderUpdate> Sent { get; } = [];

        public Task NotifyAsync(OrderUpdate update, CancellationToken cancellationToken)
        {
            Sent.Add(update);
            return Task.CompletedTask;
        }
    }

    private static readonly DateTimeOffset At = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_confirmation_becomes_an_update_with_the_order_summary()
    {
        var notifier = new RecordingNotifier();
        var customerId = Guid.NewGuid();
        var confirmed = new OrderConfirmed
        {
            EventId = Guid.NewGuid(),
            Version = 1,
            OccurredAt = At,
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-2026-000009",
            CustomerId = customerId,
            Status = "Confirmed",
            TotalAmount = 99.9m,
            Currency = "BRL",
        };

        await new OrderUpdatesBroadcaster(notifier).HandleAsync(confirmed, CancellationToken.None);

        notifier.Sent.Should().ContainSingle().Which.Should().Be(new OrderUpdate(
            confirmed.OrderId, "ORD-2026-000009", "Confirmed", At, customerId, 99.9m, "BRL", Shipment: null));
    }

    [Fact]
    public async Task A_shipment_carries_its_tracking_only_when_there_is_some()
    {
        var notifier = new RecordingNotifier();
        var broadcaster = new OrderUpdatesBroadcaster(notifier);

        await broadcaster.HandleAsync(Shipped(carrier: "Correios", code: "AB1"), CancellationToken.None);
        await broadcaster.HandleAsync(Shipped(carrier: null, code: null), CancellationToken.None);

        notifier.Sent[0].Shipment.Should().Be(new OrderUpdateShipment("Correios", "AB1", null));
        notifier.Sent[1].Shipment.Should().BeNull();
    }

    private static OrderShipped Shipped(string? carrier, string? code) => new()
    {
        EventId = Guid.NewGuid(),
        Version = 1,
        OccurredAt = At,
        OrderId = Guid.NewGuid(),
        OrderNumber = "ORD-2026-000010",
        CustomerId = Guid.NewGuid(),
        Status = "Shipped",
        TotalAmount = 10m,
        Currency = "BRL",
        Carrier = carrier,
        TrackingCode = code,
    };
}
