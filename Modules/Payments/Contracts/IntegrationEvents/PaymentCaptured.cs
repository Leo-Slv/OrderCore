using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;

/// <summary>
/// The held money was taken (Orders captures on shipping).
/// Contract <c>payments.payment-captured</c>, version 1.
/// </summary>
public sealed record PaymentCaptured : IntegrationEvent
{
    public const string Name = "payments.payment-captured";

    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
}
