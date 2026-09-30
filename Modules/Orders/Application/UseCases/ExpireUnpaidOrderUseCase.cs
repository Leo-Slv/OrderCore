using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.Telemetry;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Ends an order that has waited for payment past <c>window</c> without any
/// payment started (Docs/specs/orders/unpaid-order-expiry.md): checkout saved
/// it and reserved its stock, but starting the payment failed and nobody
/// repeated the checkout. It fails with <see cref="PaymentNotStartedReason"/>
/// through <see cref="MarkOrderPaymentFailedUseCase"/> — the same path as a
/// refused payment, so its reservations are released and
/// <c>OrderPaymentFailed</c> goes out. An order that has a payment is left to
/// the payment window; one that has moved on is left alone.
/// </summary>
public sealed class ExpireUnpaidOrderUseCase
{
    public const string PaymentNotStartedReason = "payment_not_started";

    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly MarkOrderPaymentFailedUseCase _markPaymentFailed;
    private readonly OrdersMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public ExpireUnpaidOrderUseCase(
        IOrderRepository orderRepository,
        IPaymentGateway paymentGateway,
        MarkOrderPaymentFailedUseCase markPaymentFailed,
        OrdersMetrics metrics,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _paymentGateway = paymentGateway;
        _markPaymentFailed = markPaymentFailed;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    /// <summary>The orders past the window, oldest first — each then expired on its own.</summary>
    public Task<IReadOnlyList<Guid>> FindExpiredAsync(TimeSpan window, int limit, CancellationToken cancellationToken) =>
        _orderRepository.ListPendingPaymentRequestedBeforeAsync(_timeProvider.GetUtcNow() - window, limit, cancellationToken);

    /// <returns>Whether the order was ended.</returns>
    public async Task<bool> ExpireAsync(Guid orderId, TimeSpan window, CancellationToken cancellationToken)
    {
        Observed.Order(orderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is not { Status: OrderStatus.PendingPayment, PaymentRequestedAt: { } requestedAt }
            || requestedAt >= _timeProvider.GetUtcNow() - window)
        {
            return false;
        }

        if (await _paymentGateway.GetPaymentSummaryAsync(orderId, cancellationToken) is not null)
        {
            return false;
        }

        await _markPaymentFailed.ExecuteAsync(orderId, PaymentNotStartedReason, cancellationToken);
        _metrics.UnpaidOrderExpired();
        return true;
    }
}
