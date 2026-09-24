using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
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

        stockItem.Receive(10);

        stockItem.QuantityOnHand.Should().Be(15);
    }

    [Fact]
    public void Adjust_can_increase_or_decrease_on_hand()
    {
        var stockItem = CreateStockItem(5);

        stockItem.Adjust(-2, "damaged goods");

        stockItem.QuantityOnHand.Should().Be(3);
    }

    [Fact]
    public void Adjust_rejects_a_result_below_reserved_quantity()
    {
        var stockItem = CreateStockItem(5);
        stockItem.TryReserve(5);

        var act = () => stockItem.Adjust(-1, "damaged goods");

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Adjust_requires_a_reason()
    {
        var stockItem = CreateStockItem(5);

        var act = () => stockItem.Adjust(1, "");

        act.Should().Throw<ArgumentException>();
    }
}
