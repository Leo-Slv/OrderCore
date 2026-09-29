namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <param name="RequestingCustomerId">
/// The customer cancelling their own order, or null for an admin. A customer
/// can't touch someone else's order (<c>order_not_found</c>) nor one the
/// store has started preparing (<c>order_in_fulfilment</c>).
/// </param>
/// <param name="BySystem">OrderCore itself cancels it (an expired authorization), with the admin's rules.</param>
public sealed record CancelOrderCommand(Guid OrderId, string Reason, Guid? RequestingCustomerId = null, bool BySystem = false);
