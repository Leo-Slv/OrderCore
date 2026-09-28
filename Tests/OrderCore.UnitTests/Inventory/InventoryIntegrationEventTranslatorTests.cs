using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Messaging;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Domain;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

/// <summary>An order's reservation announces each of its movements; receipts and adjustments stay internal.</summary>
public sealed class InventoryIntegrationEventTranslatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static IReadOnlyList<IntegrationEvent> Translated(IEnumerable<IDomainEvent> events) =>
        events.Select(InventoryIntegrationEventTranslator.Translate).OfType<IntegrationEvent>().ToList();

    private static IReadOnlyList<IntegrationEvent> Announced(InventoryReservation reservation, Action<InventoryReservation> transition)
    {
        reservation.ClearDomainEvents();
        transition(reservation);
        return Translated(reservation.DomainEvents);
    }

    [Fact]
    public void A_reservation_announces_each_movement_with_its_order()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var reservation = InventoryReservation.Create(productId, orderId, Guid.NewGuid(), 3, Now);

        var reserved = Translated(reservation.DomainEvents).Should().ContainSingle().Which.Should().BeOfType<StockReserved>().Subject;
        reserved.OrderId.Should().Be(orderId);
        reserved.ProductId.Should().Be(productId);
        reserved.ReservationId.Should().Be(reservation.Id);
        reserved.Quantity.Should().Be(3);

        Announced(reservation, r => r.Consume(Now)).Should().ContainSingle()
            .Which.Should().BeOfType<StockConsumed>().Which.OrderId.Should().Be(orderId);
        Announced(reservation, r => r.Return(Now)).Should().ContainSingle()
            .Which.Should().BeOfType<StockReturned>().Which.OrderId.Should().Be(orderId);
    }

    [Fact]
    public void Releasing_a_reservation_announces_stock_released()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Now);

        Announced(reservation, r => r.Release(Now)).Should().ContainSingle().Which.Should().BeOfType<StockReleased>();
    }

    [Fact]
    public void Receipts_and_adjustments_are_not_announced()
    {
        var item = StockItem.Create(Guid.NewGuid(), 10, null, Now);
        item.Receive(5, "delivery", Now);
        item.Adjust(-1, "broken", Now);

        item.DomainEvents.Should().NotBeEmpty();
        Translated(item.DomainEvents).Should().BeEmpty();
    }

    [Fact]
    public void A_stock_alert_is_announced_with_its_level()
    {
        var item = StockItem.Create(Guid.NewGuid(), 1, null, Now);
        item.ClearDomainEvents();
        item.TryReserve(1, Now);

        var alert = Translated(item.DomainEvents).Should().ContainSingle().Which.Should().BeOfType<StockAlert>().Subject;
        alert.Level.Should().Be("OutOfStock");
        alert.QuantityAvailable.Should().Be(0);
        alert.ProductId.Should().Be(item.ProductId);
    }
}
