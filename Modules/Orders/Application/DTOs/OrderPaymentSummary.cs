namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// The order's payment as the buyer sees it. <see cref="Status"/> is
/// Payments' status name (e.g. <c>Authorized</c>, <c>Failed</c>), and
/// <see cref="FailureReason"/> is the provider's reason code when it failed.
/// </summary>
public sealed record OrderPaymentSummary(Guid PaymentId, string Status, PaymentMethodChoice Method, string? FailureReason);
