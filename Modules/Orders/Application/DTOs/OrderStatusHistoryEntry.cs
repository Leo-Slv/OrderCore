namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// One recorded status transition. <see cref="FromStatus"/> is null where
/// the previous status isn't known from the event alone (see
/// <c>OrderStatusHistoryProjector</c>).
/// </summary>
public sealed record OrderStatusHistoryEntry(string? FromStatus, string ToStatus, string? Reason, DateTimeOffset ChangedAt);
