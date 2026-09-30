namespace OrderCore.Api.Modules.Payments.Application.DTOs;

/// <summary>
/// What reconciling a payment found: its status before and after, what the
/// provider said (null when the provider was asked to authorize again
/// instead), and whether OrderCore had to correct anything.
/// </summary>
public sealed record PaymentReconciliation(Guid PaymentId, string StatusBefore, string StatusAfter, string? ProviderStatus, bool Changed);
