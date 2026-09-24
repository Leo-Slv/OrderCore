using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Presentation.Requests;
using OrderCore.Api.Modules.Orders.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Domain.ValueObjects;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Orders.Presentation.Presenters;

public static class OrderPresenter
{
    public static CheckoutCommand ToCommand(CheckoutRequest request, string idempotencyKey) => new(
        request.CustomerId,
        request.Items.Select(i => new CheckoutItem(i.ProductId, i.Quantity)).ToList(),
        request.ShippingAddressId,
        request.BillingAddressId,
        request.PaymentMethod!.Value,
        idempotencyKey,
        request.CustomerNotes,
        request.ExpectedTotal);

    public static IReadOnlyList<QuoteCartLine> ToLines(QuoteCartRequest request) =>
        request.Items.Select(i => new QuoteCartLine(i.ProductId, i.Quantity, i.ExpectedUnitPrice)).ToList();

    public static OrderResponse ToResponse(OrderDetailsOutput details)
    {
        var order = details.Order;

        return new OrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt,
            ConfirmedAt = order.ConfirmedAt,
            CancelledAt = order.CancelledAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            SubtotalAmount = order.SubtotalAmount,
            DiscountAmount = order.DiscountAmount,
            ShippingAmount = order.ShippingAmount,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            ShippingAddress = ToResponse(order.ShippingAddress),
            BillingAddress = ToResponse(order.BillingAddress),
            CustomerNotes = order.CustomerNotes,
            Items = order.Items.Select(ToResponse).ToList(),
            Payment = details.Payment is { } payment
                ? new OrderPaymentResponse
                {
                    PaymentId = payment.PaymentId,
                    Status = payment.Status,
                    Method = payment.Method.ToString(),
                    FailureReason = payment.FailureReason,
                }
                : null,
        };
    }

    /// <summary>For an order whose payment status isn't needed (or can't exist yet).</summary>
    public static OrderResponse ToResponse(Order order) => ToResponse(new OrderDetailsOutput(order, Payment: null));

    public static PagedResponse<OrderSummaryResponse> ToResponse(PagedResult<OrderSummaryOutput> output) => new()
    {
        Items = output.Items.Select(o => new OrderSummaryResponse
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            Status = o.Status.ToString(),
            CreatedAt = o.CreatedAt,
            TotalAmount = o.TotalAmount,
            Currency = o.Currency,
            ItemCount = o.ItemCount,
        }).ToList(),
        Page = output.Page,
        PageSize = output.PageSize,
        TotalItems = output.TotalItems,
        TotalPages = output.TotalPages,
    };

    public static IReadOnlyList<OrderStatusHistoryEntryResponse> ToResponse(IReadOnlyList<OrderStatusHistoryEntry> history) =>
        history.Select(h => new OrderStatusHistoryEntryResponse
        {
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            Reason = h.Reason,
            ChangedAt = h.ChangedAt,
        }).ToList();

    public static CartQuoteResponse ToResponse(CartQuote quote) => new()
    {
        Currency = quote.Currency,
        Total = quote.Total,
        IsValid = quote.IsValid,
        Lines = quote.Lines.Select(l => new CartQuoteLineResponse
        {
            ProductId = l.ProductId,
            ProductName = l.ProductName,
            Slug = l.Slug,
            ImageUrl = l.ImageUrl,
            UnitPrice = l.UnitPrice,
            Quantity = l.Quantity,
            LineTotal = l.LineTotal,
            Issue = l.Issue?.ToString(),
            PreviousUnitPrice = l.PreviousUnitPrice,
        }).ToList(),
    };

    private static OrderItemResponse ToResponse(OrderItem item) => new()
    {
        ProductId = item.ProductId,
        ProductSku = item.ProductSku,
        ProductName = item.ProductName,
        ProductImageUrl = item.ProductImageUrl,
        UnitPrice = item.UnitPrice,
        Quantity = item.Quantity,
        Total = item.Total,
    };

    private static OrderAddressResponse? ToResponse(Address? address) => address is null
        ? null
        : new OrderAddressResponse
        {
            Street = address.Street,
            Number = address.Number,
            Complement = address.Complement,
            Neighborhood = address.Neighborhood,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            Country = address.Country,
        };
}
