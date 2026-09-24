namespace OrderCore.Api.Modules.Payments.Presentation.Responses;

/// <summary>
/// <see cref="FailureReason"/> is the provider's reason code (e.g.
/// <c>card_declined</c>) when <see cref="Status"/> is <c>Failed</c>, and
/// null otherwise.
/// </summary>
public sealed class PaymentResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? FailureReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? AuthorizedAt { get; init; }
}
