namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>Stock held (or once held) for one of the order's items. <see cref="Status"/> is Inventory's status name.</summary>
public sealed record OrderReservationSummary(
    Guid ReservationId,
    Guid ProductId,
    int Quantity,
    string Status,
    DateTimeOffset ReservedAt,
    DateTimeOffset? ReleasedAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset? ReturnedAt);
