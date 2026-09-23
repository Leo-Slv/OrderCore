using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

public sealed class InventoryReservationTests
{
    [Fact]
    public void Create_starts_in_Reserved_status()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), quantity: 1, DateTimeOffset.UtcNow);

        reservation.Status.Should().Be(ReservationStatus.Reserved);
    }

    [Fact]
    public void Release_then_Release_again_is_rejected()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), quantity: 1, DateTimeOffset.UtcNow);
        reservation.Release();

        var act = () => reservation.Release();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Consume_after_Expire_is_rejected()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), quantity: 1, DateTimeOffset.UtcNow);
        reservation.Expire();

        var act = () => reservation.Consume();

        act.Should().Throw<InvalidOperationException>();
    }
}

// NOTE: the concurrency scenario described in section 34 of the project
// context ("Stock = 1, 100 concurrent requests, exactly 1 succeeds") is an
// integration-level guarantee — it depends on the persistence layer
// (unique constraint / optimistic concurrency against PostgreSQL), not on
// this in-memory aggregate. That test belongs in
// OrderCore.IntegrationTests/Inventory once the EF Core mapping and
// concurrency strategy are implemented (see ADR-009, once written).
