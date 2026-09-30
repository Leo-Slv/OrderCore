namespace OrderCore.Api.Modules.Payments.Presentation.Responses;

/// <summary>
/// What <c>POST payments/{id}/reconcile</c> found. <see cref="ProviderStatus"/>
/// is where the provider says the payment stands (<c>WaitingForBuyer</c>,
/// <c>Authorized</c>, <c>Captured</c>, <c>Canceled</c>), or null when the
/// provider had never answered and was asked to authorize again.
/// <see cref="Changed"/> is whether OrderCore had to correct the payment.
/// </summary>
public sealed record PaymentReconciliationResponse(Guid PaymentId, string StatusBefore, string StatusAfter, string? ProviderStatus, bool Changed);
