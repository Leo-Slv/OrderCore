using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

/// <summary>
/// A stock alert is raised when an item enters low stock or runs out, and
/// only then: an item that stays in the same state raises nothing more.
/// </summary>
public sealed class StockAlertTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static StockItem Item(int onHand, int reorderLevel)
    {
        var item = StockItem.Create(Guid.NewGuid(), onHand, null, Now);
        item.SetReorderLevel(reorderLevel, Now);
        item.ClearDomainEvents();
        return item;
    }

    private static IEnumerable<StockAlertRaised> Alerts(StockItem item) => item.DomainEvents.OfType<StockAlertRaised>();

    [Fact]
    public void Selling_above_the_reorder_level_raises_nothing()
    {
        var item = Item(onHand: 5, reorderLevel: 3);

        item.TryReserve(1, Now);

        Alerts(item).Should().BeEmpty();
    }

    [Fact]
    public void Reaching_the_reorder_level_raises_low_stock()
    {
        var item = Item(onHand: 4, reorderLevel: 3);

        item.TryReserve(1, Now);

        var alert = Alerts(item).Should().ContainSingle().Subject;
        alert.Level.Should().Be(StockAlertLevel.LowStock);
        alert.QuantityAvailable.Should().Be(3);
        alert.ReorderLevel.Should().Be(3);
        alert.ProductId.Should().Be(item.ProductId);
    }

    [Fact]
    public void Selling_the_last_unit_raises_out_of_stock()
    {
        var item = Item(onHand: 1, reorderLevel: 3);

        item.TryReserve(1, Now);

        Alerts(item).Should().ContainSingle().Which.Level.Should().Be(StockAlertLevel.OutOfStock);
    }

    [Fact]
    public void Staying_low_raises_nothing_more()
    {
        var item = Item(onHand: 3, reorderLevel: 3);

        item.TryReserve(1, Now);

        Alerts(item).Should().BeEmpty("the item was already low on stock");
    }

    [Fact]
    public void A_refused_reservation_raises_nothing()
    {
        var item = Item(onHand: 1, reorderLevel: 3);

        item.TryReserve(2, Now).Should().BeFalse();

        Alerts(item).Should().BeEmpty();
    }

    [Fact]
    public void Raising_the_reorder_level_over_what_is_available_raises_low_stock()
    {
        var item = Item(onHand: 5, reorderLevel: 0);

        item.SetReorderLevel(5, Now);

        Alerts(item).Should().ContainSingle().Which.Level.Should().Be(StockAlertLevel.LowStock);
    }

    [Fact]
    public void Receiving_a_few_units_for_an_empty_item_turns_out_of_stock_into_low_stock()
    {
        var item = Item(onHand: 0, reorderLevel: 3);

        item.Receive(2, reason: null, Now);

        Alerts(item).Should().ContainSingle().Which.Level.Should().Be(StockAlertLevel.LowStock);
    }

    [Fact]
    public void Receiving_enough_units_raises_nothing()
    {
        var item = Item(onHand: 0, reorderLevel: 3);

        item.Receive(10, reason: null, Now);

        Alerts(item).Should().BeEmpty();
    }

    [Fact]
    public void Consuming_reserved_units_raises_nothing_since_what_is_available_does_not_change()
    {
        var item = Item(onHand: 4, reorderLevel: 3);
        item.TryReserve(4, Now);
        item.ClearDomainEvents();

        item.Consume(4);

        Alerts(item).Should().BeEmpty();
    }

    [Fact]
    public void An_adjustment_that_empties_the_item_raises_out_of_stock()
    {
        var item = Item(onHand: 10, reorderLevel: 3);

        item.Adjust(-10, "stocktake", Now);

        Alerts(item).Should().ContainSingle().Which.Level.Should().Be(StockAlertLevel.OutOfStock);
    }
}
