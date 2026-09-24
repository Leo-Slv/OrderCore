namespace OrderCore.Api.Modules.Payments.Presentation.Requests;

public sealed class CreatePaymentRequest
{
    public Guid OrderId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string IdempotencyKey { get; init; } = string.Empty;
}
