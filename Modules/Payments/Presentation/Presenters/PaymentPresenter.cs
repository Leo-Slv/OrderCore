using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Presentation.Responses;

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
        Provider = payment.Provider,
        ProviderReference = payment.ProviderReference,
        FailureReason = payment.FailureReason,
        CreatedAt = payment.CreatedAt,
        AuthorizedAt = payment.AuthorizedAt,
        CapturedAt = payment.CapturedAt,
        VoidedAt = payment.VoidedAt,
        Refunds = payment.Refunds.OrderBy(r => r.RequestedAt).Select(ToResponse).ToList(),
    };

    public static RefundResponse ToResponse(Refund refund) => new()
    {
        Id = refund.Id,
        Amount = refund.Amount,
        Reason = refund.Reason,
        Status = refund.Status.ToString(),
        RequestedAt = refund.RequestedAt,
        ProcessedAt = refund.ProcessedAt,
    };

    public static PaymentSummaryResponse ToSummaryResponse(Payment payment) => new()
    {
        Id = payment.Id,
        OrderId = payment.OrderId,
        Amount = payment.Amount,
        RefundedAmount = payment.Refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount),
        Currency = payment.Currency,
        Method = payment.Method.ToString(),
        Status = payment.Status.ToString(),
        CreatedAt = payment.CreatedAt,
    };

    public static PagedResponse<PaymentSummaryResponse> ToResponse(PagedResult<Payment> page) => new()
    {
        Items = page.Items.Select(ToSummaryResponse).ToList(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalItems = page.TotalItems,
        TotalPages = page.TotalPages,
    };
}
