namespace OrderCore.Api.Modules.Orders.Domain.Enums;

/// <summary>
/// Explicit order state machine (see section 10 of the project context).
/// Transitions are enforced by <see cref="Entities.Order"/>, never by
/// external code assigning this enum directly.
/// </summary>
public enum OrderStatus
{
    Created = 0,
    PendingPayment = 1,
    Confirmed = 2,
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    PaymentFailed = 6,
    Cancelled = 7,
}
