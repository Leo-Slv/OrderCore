using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

public sealed class InventoryReservationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static InventoryReservation CreateReservation() =>
        InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity: 1, Now);

    [Fact]
    public void Create_starts_in_Reserved_status_and_raises_a_movement_event()
    {
        var reservation = CreateReservation();

        reservation.Status.Should().Be(ReservationStatus.Reserved);
        var raised = reservation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InventoryStockMovementRecorded>().Subject;
        raised.MovementType.Should().Be(StockMovementType.ReservationCreated);
    }

    [Fact]
    public void Release_sets_ReleasedAt_and_raises_a_movement_event()
    {
        var reservation = CreateReservation();
        reservation.ClearDomainEvents();

        reservation.Release(Now);

        reservation.ReleasedAt.Should().Be(Now);
        reservation.DomainEvents.OfType<InventoryStockMovementRecorded>().Should()
            .ContainSingle(m => m.MovementType == StockMovementType.ReservationReleased);
    }

    [Fact]
    public void Release_then_Release_again_is_rejected()
    {
        var reservation = CreateReservation();
        reservation.Release(Now);

        var act = () => reservation.Release(Now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Consume_sets_ConsumedAt_and_raises_a_movement_event()
    {
        var reservation = CreateReservation();
        reservation.ClearDomainEvents();

        reservation.Consume(Now);

        reservation.ConsumedAt.Should().Be(Now);
        reservation.DomainEvents.OfType<InventoryStockMovementRecorded>().Should()
            .ContainSingle(m => m.MovementType == StockMovementType.ReservationConsumed);
    }

    [Fact]
    public void Consume_after_Expire_is_rejected()
    {
        var reservation = CreateReservation();
        reservation.Expire();

        var act = () => reservation.Consume(Now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Expire_does_not_raise_a_movement_event()
    {
        var reservation = CreateReservation();
        reservation.ClearDomainEvents();

        reservation.Expire();

        reservation.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Return_after_Consume_sets_ReturnedAt_and_raises_a_returned_movement()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity: 3, Now);
        reservation.Consume(Now);
        reservation.ClearDomainEvents();

        reservation.Return(Now);

        reservation.Status.Should().Be(ReservationStatus.Returned);
        reservation.ReturnedAt.Should().Be(Now);
        var raised = reservation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InventoryStockMovementRecorded>().Subject;
        raised.MovementType.Should().Be(StockMovementType.ReservationReturned);
        raised.Quantity.Should().Be(3);
        raised.ReferenceId.Should().Be(reservation.Id);
    }

    [Fact]
    public void Return_of_a_reservation_never_consumed_is_rejected()
    {
        var reservation = CreateReservation();

        var act = () => reservation.Return(Now);

        act.Should().Throw<DomainRuleViolationException>().Which.Code.Should().Be("invalid_reservation_state");
    }
}

// NOTE: the concurrency scenario described in section 34 of the project
// context ("Stock = 1, 100 concurrent requests, exactly 1 succeeds") is an
// integration-level guarantee — it depends on the persistence layer
// (optimistic concurrency against PostgreSQL), not on this in-memory
// aggregate. That test lives in
// OrderCore.IntegrationTests/Inventory/EfStockItemRepositoryTests.
