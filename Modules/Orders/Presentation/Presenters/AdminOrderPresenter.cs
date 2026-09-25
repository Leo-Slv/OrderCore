using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Orders.Presentation.Presenters;

/// <summary>The backoffice shapes; the customer-facing ones are in <see cref="OrderPresenter"/>.</summary>
public static class AdminOrderPresenter
{
    public static AdminOrderSummaryResponse ToResponse(AdminOrderSummaryOutput output) => new()
    {
        Id = output.Order.Id,
        OrderNumber = output.Order.OrderNumber,
        Status = output.Order.Status.ToString(),
        CreatedAt = output.Order.CreatedAt,
        TotalAmount = output.Order.TotalAmount,
        Currency = output.Order.Currency,
        ItemCount = output.Order.ItemCount,
        Customer = ToResponse(output.Customer),
        PaymentStatus = output.PaymentStatus,
    };

    public static PagedResponse<AdminOrderSummaryResponse> ToResponse(PagedResult<AdminOrderSummaryOutput> page) => new()
    {
        Items = page.Items.Select(ToResponse).ToList(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalItems = page.TotalItems,
        TotalPages = page.TotalPages,
    };

    public static AdminOrderDetailsResponse ToResponse(AdminOrderDetailsOutput output) => new()
    {
        Order = OrderPresenter.ToResponse(new OrderDetailsOutput(
            output.Order,
            output.Payment is { } payment
                ? new OrderPaymentSummary(payment.PaymentId, payment.Status, payment.Method, payment.FailureReason)
                : null)),
        InternalNotes = output.Order.InternalNotes,
        Customer = ToResponse(output.Customer),
        Payment = output.Payment is { } details ? ToResponse(details) : null,
        Reservations = output.Reservations.Select(r => new OrderReservationResponse
        {
            ReservationId = r.ReservationId,
            ProductId = r.ProductId,
            Quantity = r.Quantity,
            Status = r.Status,
            ReservedAt = r.ReservedAt,
            ReleasedAt = r.ReleasedAt,
            ConsumedAt = r.ConsumedAt,
            ReturnedAt = r.ReturnedAt,
        }).ToList(),
    };

    public static DashboardResponse ToResponse(DashboardOutput output) => new()
    {
        From = output.From,
        To = output.To,
        OrdersByStatus = output.OrdersByStatus.ToDictionary(e => e.Key.ToString(), e => e.Value),
        RevenueByCurrency = output.RevenueByCurrency,
        NewCustomers = output.NewCustomers,
        Stock = new DashboardStockResponse { LowStock = output.Stock.LowStock, OutOfStock = output.Stock.OutOfStock },
        RecentOrders = output.RecentOrders.Select(ToResponse).ToList(),
    };

    private static OrderCustomerResponse? ToResponse(OrderCustomerSnapshot? customer) =>
        customer is null
            ? null
            : new OrderCustomerResponse { Id = customer.Id, Name = customer.Name, Email = customer.Email, Active = customer.Active };

    private static OrderPaymentDetailsResponse ToResponse(OrderPaymentDetails payment) => new()
    {
        PaymentId = payment.PaymentId,
        Status = payment.Status,
        Method = payment.Method.ToString(),
        Amount = payment.Amount,
        Currency = payment.Currency,
        Provider = payment.Provider,
        ProviderReference = payment.ProviderReference,
        FailureReason = payment.FailureReason,
        CreatedAt = payment.CreatedAt,
        AuthorizedAt = payment.AuthorizedAt,
        CapturedAt = payment.CapturedAt,
        VoidedAt = payment.VoidedAt,
        Refunds = payment.Refunds.Select(r => new OrderRefundResponse
        {
            Id = r.Id,
            Amount = r.Amount,
            Reason = r.Reason,
            Status = r.Status,
            RequestedAt = r.RequestedAt,
            ProcessedAt = r.ProcessedAt,
        }).ToList(),
    };
}
