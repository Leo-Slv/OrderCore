using FluentAssertions;
using OrderCore.Api.Modules.Orders.Presentation.Realtime;
using OrderCore.Api.Shared.Application.Abstractions;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>The groups a connection joins come from its token alone.</summary>
public sealed class OrderUpdateGroupsTests
{
    private sealed record User(Guid? UserId, Guid? CustomerId, string? Role, bool EmailConfirmed = true) : ICurrentUser;

    [Fact]
    public void A_customer_follows_only_their_own_orders()
    {
        var customerId = Guid.NewGuid();

        OrderUpdateGroups.For(new User(Guid.NewGuid(), customerId, UserRoles.Customer))
            .Should().Equal($"customer:{customerId}");
    }

    [Fact]
    public void An_admin_follows_every_order()
    {
        OrderUpdateGroups.For(new User(Guid.NewGuid(), null, UserRoles.Admin)).Should().Equal(OrderUpdateGroups.Admins);
    }

    [Fact]
    public void A_customer_token_without_a_customer_joins_nothing()
    {
        OrderUpdateGroups.For(new User(Guid.NewGuid(), null, UserRoles.Customer)).Should().BeEmpty();
    }
}
