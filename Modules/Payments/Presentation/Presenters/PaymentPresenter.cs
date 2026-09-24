using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Presentation.Responses;

namespace OrderCore.Api.Modules.Payments.Presentation.Presenters;

public static class PaymentPresenter
{
    public static PaymentResponse ToResponse(Payment payment) => new()
    {
        Id = payment.Id,
        OrderId = payment.OrderId,
        Amount = payment.Amount,
        Currency = payment.Currency,
        Method = payment.Method.ToString(),
        Status = payment.Status.ToString(),
        FailureReason = payment.FailureReason,
        CreatedAt = payment.CreatedAt,
        AuthorizedAt = payment.AuthorizedAt,
    };

    public static RefundResponse ToResponse(Refund refund) => new()
    {
        Id = refund.Id,
        Amount = refund.Amount,
        Status = refund.Status.ToString(),
    };
}
