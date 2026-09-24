namespace OrderCore.Api.Modules.Payments.Presentation.Requests;

public sealed class RequestRefundRequest
{
    public decimal Amount { get; init; }

    public string Reason { get; init; } = string.Empty;
}
