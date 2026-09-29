namespace OrderCore.Api.Modules.Payments.Application.DTOs;

/// <param name="NextAction">What the buyer must do next; null when the provider answered at once.</param>
public sealed record CreatePaymentResult(Guid PaymentId, string Status, PaymentNextAction? NextAction = null);
