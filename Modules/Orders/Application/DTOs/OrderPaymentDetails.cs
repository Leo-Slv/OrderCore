namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// The order's payment in full, for the backoffice: provider details, the
/// lifecycle dates and every refund. <see cref="Status"/> is Payments'
/// status name.
/// </summary>
public sealed record OrderPaymentDetails(
    Guid PaymentId,
    string Status,
    PaymentMethodChoice Method,
    decimal Amount,
    string Currency,
    string Provider,
    string? ProviderReference,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AuthorizedAt,
    DateTimeOffset? CapturedAt,
    DateTimeOffset? VoidedAt,
    IReadOnlyList<OrderRefundSummary> Refunds);
