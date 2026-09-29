using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Payments.Domain.Entities;

/// <summary>
/// Aggregate root of the Payments bounded context (section 13). Treated as
/// logically isolated from day one so it can eventually be extracted into
/// the standalone PayCore service (section 22) without a rewrite: it does
/// not reference Order, Customer or Product entities directly, only their
/// ids. Does not raise domain events (unlike Order/InventoryReservation):
/// what Orders needs to react to is published as Integration Events via
/// the outbox (<c>IPaymentsOutbox</c>), not the in-process
/// <c>IDomainEventDispatcher</c> — see Docs/specs/payments/payment-processing.md.
/// </summary>
public sealed class Payment : AggregateRoot<Guid>
{
    private readonly List<Refund> _refunds = new();

    public Guid OrderId { get; private set; }

    public Guid? CustomerPaymentMethodId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "BRL";

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    /// <summary>
    /// Client-supplied idempotency key for the payment request (section 17).
    /// Receiving the same key twice must not authorize/capture twice.
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string? ProviderReference { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? AuthorizedAt { get; private set; }

    /// <summary>
    /// Until when the authorization can be captured: the provider's deadline
    /// (Stripe gives it per card) or <see cref="DefaultAuthorizationValidity"/>
    /// after authorizing. Past it the provider releases the money by itself.
    /// </summary>
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public DateTimeOffset? CapturedAt { get; private set; }

    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>
    /// The provider's code for the buyer's last declined attempt (e.g.
    /// <c>insufficient_funds</c>) while the payment waits for them: a decline
    /// isn't the end, the buyer may try another card (Stripe spec, decision 9).
    /// </summary>
    public string? LastDeclineReason { get; private set; }

    public DateTimeOffset? LastDeclinedAt { get; private set; }

    /// <summary>When the buyer opened a dispute (chargeback) with their bank; null if never.</summary>
    public DateTimeOffset? DisputedAt { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    private Payment()
    {
    }

    private Payment(
        Guid id, Guid orderId, decimal amount, string currency, PaymentMethod method, string idempotencyKey, string provider,
        Guid? customerPaymentMethodId, DateTimeOffset now)
        : base(id)
    {
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        Method = method;
        IdempotencyKey = idempotencyKey;
        Provider = provider;
        CustomerPaymentMethodId = customerPaymentMethodId;
        CreatedAt = now;
        UpdatedAt = now;
        Status = PaymentStatus.Pending;
    }

    public static Payment Create(
        Guid orderId, decimal amount, string currency, PaymentMethod method, string idempotencyKey, string provider,
        Guid? customerPaymentMethodId, DateTimeOffset now)
    {
        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), "Unknown payment method.");
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("A provider is required.", nameof(provider));
        }

        var payment = new Payment(Guid.NewGuid(), orderId, amount, currency, method, idempotencyKey, provider, customerPaymentMethodId, now);
        payment.IncrementVersion();
        return payment;
    }

    public void MarkProcessing()
    {
        EnsureStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Processing;
        IncrementVersion();
    }

    /// <summary>How long a card authorization lasts when the provider doesn't say (Stripe's default for online card payments).</summary>
    public static readonly TimeSpan DefaultAuthorizationValidity = TimeSpan.FromDays(7);

    /// <param name="expiresAt">The provider's capture deadline, when it gives one.</param>
    public void Authorize(string providerReference, DateTimeOffset now, DateTimeOffset? expiresAt = null)
    {
        EnsureStatus(PaymentStatus.Processing);
        Status = PaymentStatus.Authorized;
        ProviderReference = providerReference;
        AuthorizedAt = now;
        AuthorizationExpiresAt = expiresAt ?? now + DefaultAuthorizationValidity;
        UpdatedAt = now;
        IncrementVersion();
    }

    /// <summary>
    /// The provider created its side of the payment but needs the buyer to
    /// confirm it (Stripe: the card is confirmed in the browser). The payment
    /// stays <see cref="PaymentStatus.Processing"/>, now with the provider's
    /// reference, until the outcome arrives.
    /// </summary>
    public void AwaitBuyer(string providerReference, DateTimeOffset now)
    {
        EnsureStatus(PaymentStatus.Processing);
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new ArgumentException("A provider reference is required.", nameof(providerReference));
        }

        ProviderReference = providerReference;
        UpdatedAt = now;
        IncrementVersion();
    }

    /// <summary>Waiting for the buyer to confirm it at the provider (see <see cref="AwaitBuyer"/>).</summary>
    public bool IsAwaitingBuyer => Status == PaymentStatus.Processing && ProviderReference is not null;

    /// <summary>
    /// The buyer's attempt was declined; the payment keeps waiting for them to
    /// try again, and the last reason is kept for when it finally fails.
    /// </summary>
    public void RecordDecline(string reason, DateTimeOffset at)
    {
        if (!IsAwaitingBuyer)
        {
            throw new DomainRuleViolationException("invalid_payment_state", $"Payment '{Id}' is not waiting for the buyer.");
        }

        LastDeclineReason = string.IsNullOrWhiteSpace(reason) ? "declined" : reason;
        LastDeclinedAt = at;
        UpdatedAt = at;
        IncrementVersion();
    }

    public bool IsDisputed => DisputedAt is not null;

    /// <summary>Records that the buyer disputed the charge. Idempotent: false if it already was.</summary>
    public bool MarkDisputed(DateTimeOffset at)
    {
        if (IsDisputed)
        {
            return false;
        }

        DisputedAt = at;
        UpdatedAt = at;
        IncrementVersion();
        return true;
    }

    /// <summary>
    /// The provider settled a refund that was still pending: completed (and,
    /// once completed refunds cover the whole amount, the payment is
    /// <see cref="PaymentStatus.Refunded"/>) or failed. False when the refund
    /// isn't pending any more — the update arrived late or twice.
    /// </summary>
    public bool SettleRefund(Guid refundId, bool succeeded, string? failureReason, DateTimeOffset now)
    {
        var refund = _refunds.SingleOrDefault(r => r.Id == refundId);
        if (refund is null || refund.Status != RefundStatus.Pending)
        {
            return false;
        }

        if (succeeded)
        {
            refund.Complete(now);
            if (Status == PaymentStatus.Captured
                && _refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount) >= Amount)
            {
                Status = PaymentStatus.Refunded;
            }
        }
        else
        {
            refund.Fail(failureReason ?? "refund_failed", now);
        }

        UpdatedAt = now;
        IncrementVersion();
        return true;
    }

    public void Capture(DateTimeOffset now)
    {
        EnsureStatus(PaymentStatus.Authorized);
        Status = PaymentStatus.Captured;
        CapturedAt = now;
        IncrementVersion();
    }

    /// <summary>
    /// Releases an authorization that was never captured, so the buyer is
    /// never charged (backoffice decision 2: cancelling a paid order). A
    /// captured payment is refunded instead. A payment still waiting for the
    /// buyer ends the same way once the provider has cancelled it: nothing
    /// was charged or held.
    /// </summary>
    public void Void(DateTimeOffset now)
    {
        if (!IsAwaitingBuyer)
        {
            EnsureStatus(PaymentStatus.Authorized);
        }

        Status = PaymentStatus.Voided;
        VoidedAt = now;
        IncrementVersion();
    }

    public void Fail(string reason)
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Refunded or PaymentStatus.Voided)
        {
            throw new DomainRuleViolationException("invalid_payment_state", $"Cannot fail a payment in status '{Status}'.");
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

    /// <summary>
    /// <paramref name="now"/> is not in 06-payments.md's signature, but
    /// <c>Refund.RequestedAt</c> needs a value — same class of gap as
    /// <c>Customer.Create</c> gaining `now`. Validates the refundable-
    /// balance invariant from the diagram's "Invariante a confirmar": the
    /// requested amount plus every already-granted (non-failed) refund can
    /// never exceed <see cref="Amount"/>.
    /// </summary>
    // Fully qualified return type: this class also has a method named
    // `Refund` (matching 06-payments.md exactly), which shadows the type
    // name `Refund` within the class body — a naming collision the diagram
    // itself has, not a design change.
    public global::OrderCore.Api.Modules.Payments.Domain.Entities.Refund RequestRefund(decimal amount, string reason, DateTimeOffset now)
    {
        EnsureStatus(PaymentStatus.Captured);

        var alreadyRefunded = _refunds.Where(r => r.Status != RefundStatus.Failed).Sum(r => r.Amount);
        if (amount > Amount - alreadyRefunded)
        {
            throw new DomainRuleViolationException("refund_exceeds_balance", "Refund amount exceeds the payment's refundable balance.");
        }

        var refund = global::OrderCore.Api.Modules.Payments.Domain.Entities.Refund.Create(amount, reason, now);
        _refunds.Add(refund);
        IncrementVersion();
        return refund;
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainRuleViolationException(
                "invalid_payment_state",
                $"Cannot transition payment '{Id}' from '{Status}' as if it were '{expected}'.");
        }
    }

    /// <summary>
    /// Reconstructs a <see cref="Payment"/> from already-persisted state,
    /// distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module).
    /// </summary>
    internal static Payment Rehydrate(
        Guid id,
        Guid orderId,
        Guid? customerPaymentMethodId,
        decimal amount,
        string currency,
        PaymentMethod method,
        PaymentStatus status,
        string idempotencyKey,
        string provider,
        string? providerReference,
        string? failureReason,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? authorizedAt,
        DateTimeOffset? capturedAt,
        DateTimeOffset? voidedAt,
        int version,
        IEnumerable<Refund> refunds,
        string? lastDeclineReason = null,
        DateTimeOffset? lastDeclinedAt = null,
        DateTimeOffset? disputedAt = null,
        DateTimeOffset? authorizationExpiresAt = null)
    {
        var payment = new Payment(id, orderId, amount, currency, method, idempotencyKey, provider, customerPaymentMethodId, createdAt)
        {
            Status = status,
            ProviderReference = providerReference,
            FailureReason = failureReason,
            UpdatedAt = updatedAt,
            AuthorizedAt = authorizedAt,
            CapturedAt = capturedAt,
            VoidedAt = voidedAt,
            LastDeclineReason = lastDeclineReason,
            LastDeclinedAt = lastDeclinedAt,
            DisputedAt = disputedAt,
            AuthorizationExpiresAt = authorizationExpiresAt,
            Version = version,
        };

        payment._refunds.AddRange(refunds);

        return payment;
    }
}
