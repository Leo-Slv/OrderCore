using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;

/// <summary>
/// A refund of the payment went through.
/// Contract <c>payments.payment-refunded</c>, version 1.
/// </summary>
public sealed record PaymentRefunded : IntegrationEvent
{
    public const string Name = "payments.payment-refunded";

    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }
}
