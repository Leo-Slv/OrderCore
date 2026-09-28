using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;

/// <summary>
/// The provider refused the payment (Orders marks the order as payment failed).
/// Contract <c>payments.payment-failed</c>, version 1.
/// </summary>
public sealed record PaymentFailed : IntegrationEvent
{
    public const string Name = "payments.payment-failed";

    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }

    public required string Reason { get; init; }
}
