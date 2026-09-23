using FluentAssertions;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>
/// OrderItem's own mutators are `internal` (only Order can call them), so
/// its behavior is exercised through Order.DecreaseItemQuantity/
/// ApplyItemDiscount, the same way AddItem/RemoveItem already are in
/// OrderTests.
/// </summary>
public sealed class OrderItemTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static Order CreateOrderWithItem(int quantity = 3)
    {
        var order = Order.Create(CustomerId, "BRL", "ORD-2026-000001", DateTimeOffset.UtcNow);
        order.AddItem(ProductId, productVariantId: null, "SKU-1", "Widget", productImageUrl: null, unitPrice: 10m, quantity);
        return order;
    }

    [Fact]
    public void DecreaseItemQuantity_reduces_quantity()
    {
        var order = CreateOrderWithItem(3);

        order.DecreaseItemQuantity(ProductId, 1);

        order.Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public void DecreaseItemQuantity_to_zero_or_below_throws()
    {
        var order = CreateOrderWithItem(3);

        var act = () => order.DecreaseItemQuantity(ProductId, 3);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ApplyItemDiscount_reduces_item_Total()
    {
        var order = CreateOrderWithItem(2);

        order.ApplyItemDiscount(ProductId, 5m);

        order.Items.Single().Total.Should().Be((10m * 2) - 5m);
    }

    [Fact]
    public void ApplyItemDiscount_rejects_more_than_the_full_price()
    {
        var order = CreateOrderWithItem(2);

        var act = () => order.ApplyItemDiscount(ProductId, 21m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
