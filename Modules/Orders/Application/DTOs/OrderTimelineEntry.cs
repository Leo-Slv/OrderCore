namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// Something that happened to an order in any module: <see cref="Type"/> is
/// the event's contract name (e.g. <c>payments.payment-authorized</c>),
/// <see cref="Source"/> the module that published it, and
/// <see cref="Details"/> a few facts worth showing (amount and currency,
/// reason, product and quantity).
/// </summary>
public sealed record OrderTimelineEntry(
    Guid EventId, string Type, string Source, DateTimeOffset OccurredAt, IReadOnlyDictionary<string, string?> Details);
