using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Payments.Domain.Entities;

/// <summary>
/// A refund requested against a <see cref="Payment"/>. The refundable-
/// balance invariant (a refund can't exceed what's left to refund) lives
/// in <see cref="Payment.RequestRefund"/>, the aggregate that actually
/// knows the full amount and every previously granted refund — see
/// 06-payments.md's "Invariante a confirmar".
/// </summary>
public sealed class Refund : Entity<Guid>
{
    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public RefundStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    private Refund()
    {
    }

    private Refund(Guid id, decimal amount, string reason, DateTimeOffset requestedAt) : base(id)
    {
        Amount = amount;
        Reason = reason;
        RequestedAt = requestedAt;
        Status = RefundStatus.Pending;
    }

    internal static Refund Create(decimal amount, string reason, DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required.", nameof(reason));
        }

        return new Refund(Guid.NewGuid(), amount, reason, now);
    }

    /// <summary>
    /// <paramref name="now"/> is not in 06-payments.md's signature, but
    /// <see cref="ProcessedAt"/> needs a value — same class of gap as
    /// <c>Customer.Create</c> gaining `now`.
    /// </summary>
    public void Complete(DateTimeOffset now)
    {
        EnsureStatus(RefundStatus.Pending);
        Status = RefundStatus.Completed;
        ProcessedAt = now;
    }

    /// <summary>
    /// <paramref name="now"/> — see <see cref="Complete"/>'s remarks.
    /// Refund has no separate FailureReason field (unlike Payment), so
    /// <paramref name="reason"/> overwrites the original request reason
    /// with why the refund failed.
    /// </summary>
    public void Fail(string reason, DateTimeOffset now)
    {
        EnsureStatus(RefundStatus.Pending);
        Status = RefundStatus.Failed;
        Reason = reason;
        ProcessedAt = now;
    }

    private void EnsureStatus(RefundStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Cannot transition refund '{Id}' from '{Status}' as if it were '{expected}'.");
        }
    }

    /// <summary>
    /// Reconstructs a <see cref="Refund"/> from already-persisted state,
    /// distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module).
    /// </summary>
    internal static Refund Rehydrate(Guid id, decimal amount, string reason, RefundStatus status, DateTimeOffset requestedAt, DateTimeOffset? processedAt)
    {
        return new Refund(id, amount, reason, requestedAt)
        {
            Status = status,
            ProcessedAt = processedAt,
        };
    }
}
