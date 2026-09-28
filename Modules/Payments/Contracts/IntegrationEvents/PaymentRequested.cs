using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;

/// <summary>
/// A payment was created for an order and sent to the provider.
/// Contract <c>payments.payment-requested</c>, version 1.
/// </summary>
public sealed record PaymentRequested : IntegrationEvent
{
    public const string Name = "payments.payment-requested";

    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string IdempotencyKey { get; init; }
}
