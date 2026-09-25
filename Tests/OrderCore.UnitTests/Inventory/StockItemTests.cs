using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

public sealed class StockItemTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static StockItem CreateStockItem(int initialQuantity = 10) =>
        StockItem.Create(Guid.NewGuid(), initialQuantity, null, Now);

    [Fact]
    public void Create_starts_with_nothing_reserved()
    {
        var stockItem = CreateStockItem(10);

        stockItem.QuantityOnHand.Should().Be(10);
        stockItem.QuantityReserved.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(10);
    }

    [Fact]
    public void IsLowStock_is_false_with_the_default_reorder_level()
    {
        var stockItem = CreateStockItem(3);

        stockItem.IsLowStock.Should().BeFalse();
    }

    [Fact]
    public void IsLowStock_is_false_when_nothing_is_available()
    {
        var stockItem = CreateStockItem(1);
        stockItem.TryReserve(1);

        stockItem.IsLowStock.Should().BeFalse();
    }

    [Fact]
    public void TryReserve_succeeds_when_enough_is_available()
    {
        var stockItem = CreateStockItem(1);

        var succeeded = stockItem.TryReserve(1);

        succeeded.Should().BeTrue();
        stockItem.QuantityAvailable.Should().Be(0);
    }

    [Fact]
    public void TryReserve_fails_without_mutating_state_when_not_enough_is_available()
    {
        var stockItem = CreateStockItem(1);
        stockItem.TryReserve(1);

        var succeeded = stockItem.TryReserve(1);

        succeeded.Should().BeFalse();
        stockItem.QuantityReserved.Should().Be(1);
    }

    [Fact]
    public void Release_frees_up_reserved_quantity()
    {
        var stockItem = CreateStockItem(5);
        stockItem.TryReserve(3);

        stockItem.Release(3);

        stockItem.QuantityReserved.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(5);
    }

    [Fact]
    public void Release_more_than_reserved_throws()
    {
        var stockItem = CreateStockItem(5);

        var act = () => stockItem.Release(1);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Consume_reduces_both_on_hand_and_reserved()
    {
        var stockItem = CreateStockItem(5);
        stockItem.TryReserve(2);

        stockItem.Consume(2);

        stockItem.QuantityOnHand.Should().Be(3);
        stockItem.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void Receive_increases_quantity_on_hand()
    {
        var stockItem = CreateStockItem(5);

        stockItem.Receive(10, "supplier delivery", Now);

        stockItem.QuantityOnHand.Should().Be(15);
    }

    [Fact]
    public void Adjust_can_increase_or_decrease_on_hand()
    {
        var stockItem = CreateStockItem(5);

        stockItem.Adjust(-2, "damaged goods", Now);

        stockItem.QuantityOnHand.Should().Be(3);
    }

    [Fact]
    public void Adjust_rejects_a_result_below_reserved_quantity()
    {
        var stockItem = CreateStockItem(5);
        stockItem.TryReserve(5);

        var act = () => stockItem.Adjust(-1, "damaged goods", Now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Adjust_requires_a_reason()
    {
        var stockItem = CreateStockItem(5);

        var act = () => stockItem.Adjust(1, "", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_reorder_level_makes_low_stock_reachable()
    {
        var stockItem = CreateStockItem(3);

        stockItem.SetReorderLevel(5, Now);

        stockItem.ReorderLevel.Should().Be(5);
        stockItem.IsLowStock.Should().BeTrue();
        stockItem.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void A_negative_reorder_level_is_rejected()
    {
        var act = () => CreateStockItem().SetReorderLevel(-1, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_with_units_records_them_as_inbound_and_an_empty_one_records_nothing()
    {
        CreateStockItem(4).DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InventoryStockMovementRecorded>()
            .Which.MovementType.Should().Be(StockMovementType.Inbound);
        CreateStockItem(0).DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Receive_records_an_inbound_movement_with_its_reason()
    {
        var stockItem = CreateStockItem(0);

        stockItem.Receive(7, "  invoice 123  ", Now);

        var movement = stockItem.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InventoryStockMovementRecorded>().Subject;
        movement.MovementType.Should().Be(StockMovementType.Inbound);
        movement.Quantity.Should().Be(7);
        movement.Reason.Should().Be("invoice 123");
        movement.ReferenceId.Should().Be(stockItem.Id);
    }

    [Fact]
    public void Adjust_records_a_signed_adjustment_with_its_reason()
    {
        var stockItem = CreateStockItem(0);
        stockItem.Receive(5, null, Now);
        stockItem.ClearDomainEvents();

        stockItem.Adjust(-2, "damaged goods", Now);

        var movement = stockItem.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InventoryStockMovementRecorded>().Subject;
        movement.MovementType.Should().Be(StockMovementType.Adjustment);
        movement.Quantity.Should().Be(-2);
        movement.Reason.Should().Be("damaged goods");
    }

    [Fact]
    public void A_reason_longer_than_the_limit_is_rejected_before_anything_changes()
    {
        var stockItem = CreateStockItem(5);
        var tooLong = new string('x', StockItem.MaxReasonLength + 1);

        stockItem.Invoking(s => s.Receive(1, tooLong, Now)).Should().Throw<ArgumentException>();
        stockItem.Invoking(s => s.Adjust(1, tooLong, Now)).Should().Throw<ArgumentException>();
        stockItem.QuantityOnHand.Should().Be(5);
    }

    [Fact]
    public void ReturnConsumed_puts_units_back_without_recording_a_movement_itself()
    {
        var stockItem = CreateStockItem(0);

        stockItem.ReturnConsumed(2, Now);

        stockItem.QuantityOnHand.Should().Be(2);
        stockItem.DomainEvents.Should().BeEmpty();
    }
}
