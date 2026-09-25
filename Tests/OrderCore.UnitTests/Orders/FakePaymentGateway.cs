using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.UnitTests.Orders;

internal sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly Dictionary<Guid, OrderPaymentSummary> _payments = new();

    public List<(Guid OrderId, decimal Amount, PaymentMethodChoice Method)> Requests { get; } = new();

    /// <summary>Makes the next payment request fail, as an unavailable provider would.</summary>
    public Exception? FailNextRequestWith { get; set; }

    public Task<Guid> RequestPaymentAsync(
        Guid orderId,
        decimal amount,
        string currency,
        PaymentMethodChoice method,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (FailNextRequestWith is { } failure)
        {
            FailNextRequestWith = null;
            throw failure;
        }

        Requests.Add((orderId, amount, method));
        var paymentId = Guid.NewGuid();
        _payments[orderId] = new OrderPaymentSummary(paymentId, "Authorized", method, FailureReason: null);
        return Task.FromResult(paymentId);
    }

    public Task<OrderPaymentSummary?> GetPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult(_payments.GetValueOrDefault(orderId));

    /// <summary>Makes the next capture fail, as a refusing provider would.</summary>
    public Exception? FailNextCaptureWith { get; set; }

    /// <summary>Makes the next settlement fail, e.g. a payment still with the provider.</summary>
    public Exception? FailNextSettlementWith { get; set; }

    public List<Guid> Captured { get; } = new();

    public List<(Guid OrderId, string Reason)> Settlements { get; } = new();

    public OrderPaymentSettlement SettlementOutcome { get; set; } = OrderPaymentSettlement.Voided;

    public Task<IReadOnlyDictionary<Guid, OrderPaymentSummary>> GetPaymentSummariesAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, OrderPaymentSummary>>(
            _payments.Where(p => orderIds.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));

    public Task<OrderPaymentDetails?> GetPaymentDetailsAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult<OrderPaymentDetails?>(null);

    public Task CaptureForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (FailNextCaptureWith is { } failure)
        {
            FailNextCaptureWith = null;
            throw failure;
        }

        Captured.Add(orderId);
        return Task.CompletedTask;
    }

    public Task<OrderPaymentSettlement> SettleForCancellationAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        if (FailNextSettlementWith is { } failure)
        {
            FailNextSettlementWith = null;
            throw failure;
        }

        Settlements.Add((orderId, reason));
        return Task.FromResult(SettlementOutcome);
    }
}
