namespace OrderCore.Api.Modules.Payments.Presentation.Responses;

public sealed class RefundResponse
{
    public Guid Id { get; init; }

    public decimal Amount { get; init; }

    public string Status { get; init; } = string.Empty;
}
