using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' own <see cref="IPaymentGateway"/> by wrapping
/// Payments' use cases — the "Application Contract" indirection from
/// section 7, same pattern as <c>ProductCatalogAdapter</c>/
/// <c>InventoryServiceAdapter</c>. Maps Payments' types to Orders' own
/// (<see cref="PaymentMethodChoice"/>, <see cref="OrderPaymentSummary"/>,
/// <see cref="OrderPaymentDetails"/>, <see cref="OrderPaymentSettlement"/>).
/// See 05-orders.md.
/// </summary>
public sealed class PaymentGatewayAdapter : IPaymentGateway
{
    private readonly CreatePaymentUseCase _createPayment;
    private readonly GetPaymentByOrderIdUseCase _getPaymentByOrderId;
    private readonly GetPaymentsByOrderIdsUseCase _getPaymentsByOrderIds;
    private readonly CapturePaymentUseCase _capturePayment;
    private readonly SettlePaymentForCancellationUseCase _settlePayment;

    public PaymentGatewayAdapter(
        CreatePaymentUseCase createPayment,
        GetPaymentByOrderIdUseCase getPaymentByOrderId,
        GetPaymentsByOrderIdsUseCase getPaymentsByOrderIds,
        CapturePaymentUseCase capturePayment,
        SettlePaymentForCancellationUseCase settlePayment)
    {
        _createPayment = createPayment;
        _getPaymentByOrderId = getPaymentByOrderId;
        _getPaymentsByOrderIds = getPaymentsByOrderIds;
        _capturePayment = capturePayment;
        _settlePayment = settlePayment;
    }

    public async Task<Guid> RequestPaymentAsync(
        Guid orderId,
        decimal amount,
        string currency,
        PaymentMethodChoice method,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new CreatePaymentCommand(orderId, amount, currency, ToPaymentMethod(method), idempotencyKey);
        var result = await _createPayment.ExecuteAsync(command, cancellationToken);
        return result.PaymentId;
    }

    public async Task<OrderPaymentSummary?> GetPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _getPaymentByOrderId.ExecuteAsync(orderId, cancellationToken);
        return payment is null ? null : ToSummary(payment);
    }

    public async Task<IReadOnlyDictionary<Guid, OrderPaymentSummary>> GetPaymentSummariesAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        var payments = await _getPaymentsByOrderIds.ExecuteAsync(orderIds, cancellationToken);

        // One payment per order today; should that ever change, the latest wins.
        return payments
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => ToSummary(g.OrderByDescending(p => p.CreatedAt).First()));
    }

    public async Task<OrderPaymentDetails?> GetPaymentDetailsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _getPaymentByOrderId.ExecuteAsync(orderId, cancellationToken);

        return payment is null
            ? null
            : new OrderPaymentDetails(
                payment.Id,
                payment.Status.ToString(),
                ToChoice(payment.Method),
                payment.Amount,
                payment.Currency,
                payment.Provider,
                payment.ProviderReference,
                payment.FailureReason,
                payment.CreatedAt,
                payment.AuthorizedAt,
                payment.CapturedAt,
                payment.VoidedAt,
                payment.Refunds
                    .OrderBy(r => r.RequestedAt)
                    .Select(r => new OrderRefundSummary(r.Id, r.Amount, r.Reason, r.Status.ToString(), r.RequestedAt, r.ProcessedAt))
                    .ToList());
    }

    public Task CaptureForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        _capturePayment.ExecuteForOrderAsync(orderId, cancellationToken);

    public async Task<OrderPaymentSettlement> SettleForCancellationAsync(Guid orderId, string reason, CancellationToken cancellationToken) =>
        await _settlePayment.ExecuteAsync(orderId, reason, cancellationToken) switch
        {
            PaymentSettlementOutcome.Voided => OrderPaymentSettlement.Voided,
            PaymentSettlementOutcome.Refunded => OrderPaymentSettlement.Refunded,
            _ => OrderPaymentSettlement.NothingToSettle,
        };

    private static OrderPaymentSummary ToSummary(Payment payment) =>
        new(payment.Id, payment.Status.ToString(), ToChoice(payment.Method), payment.FailureReason);

    private static PaymentMethod ToPaymentMethod(PaymentMethodChoice choice) => choice switch
    {
        PaymentMethodChoice.Card => PaymentMethod.Card,
        PaymentMethodChoice.Pix => PaymentMethod.Pix,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown payment method."),
    };

    private static PaymentMethodChoice ToChoice(PaymentMethod method) => method switch
    {
        PaymentMethod.Card => PaymentMethodChoice.Card,
        PaymentMethod.Pix => PaymentMethodChoice.Pix,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown payment method."),
    };
}
