using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;

/// <summary>
/// The authorization expired before the order shipped (Stripe cancels an
/// uncaptured card authorization after about seven days): nothing will be
/// charged, so the order can't ship as it is (Stripe spec, decision 6).
/// Published together with <see cref="PaymentVoided"/>.
/// Contract <c>payments.payment-authorization-expired</c>, version 1.
/// </summary>
public sealed record PaymentAuthorizationExpired : IntegrationEvent
{
    public const string Name = "payments.payment-authorization-expired";

    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }
}
