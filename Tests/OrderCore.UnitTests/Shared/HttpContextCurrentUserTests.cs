using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Presentation.Authentication;
using Xunit;

namespace OrderCore.UnitTests.Shared;

public sealed class HttpContextCurrentUserTests
{
    private static ICurrentUser CurrentUserFor(ClaimsPrincipal? principal)
    {
        var accessor = new HttpContextAccessor();
        if (principal is not null)
        {
            accessor.HttpContext = new DefaultHttpContext { User = principal };
        }

        return new HttpContextCurrentUser(accessor);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

    [Fact]
    public void Reads_a_signed_in_customer_from_the_token_claims()
    {
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var currentUser = CurrentUserFor(Authenticated(
            new Claim(OrderCoreClaimTypes.UserId, userId.ToString()),
            new Claim(OrderCoreClaimTypes.Role, UserRoles.Customer),
            new Claim(OrderCoreClaimTypes.CustomerId, customerId.ToString())));

        currentUser.UserId.Should().Be(userId);
        currentUser.CustomerId.Should().Be(customerId);
        currentUser.IsAuthenticated.Should().BeTrue();
        currentUser.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void Reads_an_admin_without_a_customer()
    {
        var currentUser = CurrentUserFor(Authenticated(
            new Claim(OrderCoreClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(OrderCoreClaimTypes.Role, UserRoles.Admin)));

        currentUser.IsAdmin.Should().BeTrue();
        currentUser.CustomerId.Should().BeNull();
    }

    [Fact]
    public void Is_nobody_outside_a_request()
    {
        var currentUser = CurrentUserFor(principal: null);

        currentUser.UserId.Should().BeNull();
        currentUser.Role.Should().BeNull();
        currentUser.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void Ignores_claims_of_an_unauthenticated_principal()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(OrderCoreClaimTypes.UserId, Guid.NewGuid().ToString())]));

        CurrentUserFor(anonymous).UserId.Should().BeNull();
    }

    [Fact]
    public void Treats_a_malformed_id_claim_as_absent()
    {
        var currentUser = CurrentUserFor(Authenticated(new Claim(OrderCoreClaimTypes.UserId, "not-a-guid")));

        currentUser.UserId.Should().BeNull();
    }
}
