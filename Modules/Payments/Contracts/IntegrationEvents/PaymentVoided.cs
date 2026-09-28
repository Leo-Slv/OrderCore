using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;

/// <summary>
/// The authorization was released without charging the buyer (an order cancelled before shipping).
/// Contract <c>payments.payment-voided</c>, version 1.
/// </summary>
public sealed record PaymentVoided : IntegrationEvent
{
    public const string Name = "payments.payment-voided";

    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }
}
