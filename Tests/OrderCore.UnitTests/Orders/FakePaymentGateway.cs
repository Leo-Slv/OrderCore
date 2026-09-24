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
}
