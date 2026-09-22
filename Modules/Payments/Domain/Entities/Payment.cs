using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Payments.Domain.Entities;

/// <summary>
/// Aggregate root of the Payments bounded context (section 13). Treated as
/// logically isolated from day one so it can eventually be extracted into
/// the standalone PayCore service (section 22) without a rewrite: it does
/// not reference Order, Customer or Product entities directly, only their
/// ids.
/// </summary>
public sealed class Payment : AggregateRoot<Guid>
{
    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "BRL";

    public PaymentStatus Status { get; private set; }

    /// <summary>
    /// Client-supplied idempotency key for the payment request (section 17).
    /// Receiving the same key twice must not authorize/capture twice.
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? ProviderReference { get; private set; }

    public string? FailureReason { get; private set; }

    private Payment()
    {
    }

    private Payment(Guid id, Guid orderId, decimal amount, string currency, string idempotencyKey)
        : base(id)
    {
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
        Status = PaymentStatus.Pending;
    }

    public static Payment Create(Guid orderId, decimal amount, string currency, string idempotencyKey)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        }

        var payment = new Payment(Guid.NewGuid(), orderId, amount, currency, idempotencyKey);
        payment.IncrementVersion();
        return payment;
    }

    public void MarkProcessing()
    {
        EnsureStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Processing;
        IncrementVersion();
    }

    public void Authorize(string providerReference)
    {
        EnsureStatus(PaymentStatus.Processing);
        Status = PaymentStatus.Authorized;
        ProviderReference = providerReference;
        IncrementVersion();
    }

    public void Capture()
    {
        EnsureStatus(PaymentStatus.Authorized);
        Status = PaymentStatus.Captured;
        IncrementVersion();
    }

    public void Fail(string reason)
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Refunded)
        {
            throw new InvalidOperationException($"Cannot fail a payment in status '{Status}'.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        IncrementVersion();
    }

    public void Refund()
    {
        EnsureStatus(PaymentStatus.Captured);
        Status = PaymentStatus.Refunded;
        IncrementVersion();
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Cannot transition payment '{Id}' from '{Status}' as if it were '{expected}'.");
        }
    }
}
