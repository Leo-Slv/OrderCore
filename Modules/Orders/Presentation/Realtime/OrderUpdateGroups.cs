using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Orders.Presentation.Realtime;

/// <summary>
/// Who receives which order updates, decided from the token only — the
/// client never asks to follow an order: a customer is in
/// <c>customer:{customerId}</c> and gets their own orders' updates, an admin
/// is in <see cref="Admins"/> and gets everyone's. Nothing to check per
/// order, nothing to probe.
/// </summary>
public static class OrderUpdateGroups
{
    public const string Admins = "admins";

    public static string Customer(Guid customerId) => $"customer:{customerId}";

    /// <summary>The groups a connection joins; none for a signed-in account that is neither.</summary>
    public static IReadOnlyList<string> For(ICurrentUser user) =>
        user.Role == UserRoles.Admin ? [Admins]
        : user.Role == UserRoles.Customer && user.CustomerId is { } customerId ? [Customer(customerId)]
        : [];
}
