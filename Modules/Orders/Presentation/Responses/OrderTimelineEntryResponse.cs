namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// One event in the order's timeline. <c>type</c> is the event's contract
/// name (e.g. <c>payments.payment-authorized</c>), <c>source</c> the module
/// that published it; <c>details</c> holds what is worth showing for that
/// type (<c>amount</c>/<c>currency</c>, <c>reason</c>, <c>productId</c>/<c>quantity</c>).
/// </summary>
public sealed record OrderTimelineEntryResponse(
    Guid EventId, string Type, string Source, DateTimeOffset OccurredAt, IReadOnlyDictionary<string, string?> Details);
