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
        Status = payment.Status.ToString(),
    };

    public static RefundResponse ToResponse(Refund refund) => new()
    {
        Id = refund.Id,
        Amount = refund.Amount,
        Status = refund.Status.ToString(),
    };
}
