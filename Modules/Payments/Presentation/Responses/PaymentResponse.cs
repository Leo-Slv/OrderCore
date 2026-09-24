namespace OrderCore.Api.Modules.Payments.Presentation.Responses;

public sealed class PaymentResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public decimal Amount { get; init; }

    public string Status { get; init; } = string.Empty;
}
