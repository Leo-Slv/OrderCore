using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;

/// <summary>
/// The provider authorized the payment: the money is held, not yet taken (Orders confirms the order).
/// Contract <c>payments.payment-authorized</c>, version 1.
/// </summary>
public sealed record PaymentAuthorized : IntegrationEvent
{
    public const string Name = "payments.payment-authorized";

    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
}
