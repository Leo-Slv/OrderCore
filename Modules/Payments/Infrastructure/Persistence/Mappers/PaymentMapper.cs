using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Infrastructure.Persistence;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between the <see cref="Payment"/> aggregate and its
/// persistence models — see <c>CustomerMapper</c>'s remarks on
/// <c>ToDomain</c> going through <c>Rehydrate</c>, not <c>Create</c>.
/// </summary>
public static class PaymentMapper
{
    public static Payment ToDomain(PaymentPersistenceModel model)
    {
        var refunds = model.Refunds.Select(ToDomain);

        return Payment.Rehydrate(
            model.Id,
            model.OrderId,
            model.CustomerPaymentMethodId,
            model.Amount,
            model.Currency,
            Enum.Parse<PaymentMethod>(model.Method),
            Enum.Parse<PaymentStatus>(model.Status),
            model.IdempotencyKey,
            model.Provider,
            model.ProviderReference,
            model.FailureReason,
            model.CreatedAt,
            model.UpdatedAt,
            model.AuthorizedAt,
            model.CapturedAt,
            model.VoidedAt,
            model.Version,
            refunds);
    }

    public static PaymentPersistenceModel ToPersistence(Payment domain) => new()
    {
        Id = domain.Id,
        OrderId = domain.OrderId,
        CustomerPaymentMethodId = domain.CustomerPaymentMethodId,
        Amount = domain.Amount,
        Currency = domain.Currency,
        Method = domain.Method.ToString(),
        Status = domain.Status.ToString(),
        IdempotencyKey = domain.IdempotencyKey,
        Provider = domain.Provider,
        ProviderReference = domain.ProviderReference,
        FailureReason = domain.FailureReason,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
        AuthorizedAt = domain.AuthorizedAt,
        CapturedAt = domain.CapturedAt,
        VoidedAt = domain.VoidedAt,
        Version = domain.Version,
        Refunds = domain.Refunds.Select(ToPersistence).ToList(),
    };

    public static void ApplyChanges(Payment domain, PaymentPersistenceModel model)
    {
        model.Status = domain.Status.ToString();
        model.ProviderReference = domain.ProviderReference;
        model.FailureReason = domain.FailureReason;
        model.UpdatedAt = domain.UpdatedAt;
        model.AuthorizedAt = domain.AuthorizedAt;
        model.CapturedAt = domain.CapturedAt;
        model.VoidedAt = domain.VoidedAt;
        model.Version = domain.Version;

        ChildCollectionReconciler.Reconcile(domain.Refunds, model.Refunds, ToPersistence, ApplyChanges, r => r.Id);
    }

    private static RefundPersistenceModel ToPersistence(Refund domain) => new()
    {
        Id = domain.Id,
        Amount = domain.Amount,
        Reason = domain.Reason,
        Status = domain.Status.ToString(),
        RequestedAt = domain.RequestedAt,
        ProcessedAt = domain.ProcessedAt,
    };

    private static void ApplyChanges(Refund domain, RefundPersistenceModel model)
    {
        model.Reason = domain.Reason;
        model.Status = domain.Status.ToString();
        model.ProcessedAt = domain.ProcessedAt;
    }

    private static Refund ToDomain(RefundPersistenceModel model) => Refund.Rehydrate(
        model.Id, model.Amount, model.Reason, Enum.Parse<RefundStatus>(model.Status), model.RequestedAt, model.ProcessedAt);
}
