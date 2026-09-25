namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;

/// <summary>
/// Expanded beyond 06-payments.md's abbreviated shape with every
/// <see cref="Domain.Entities.Payment"/> field (Provider,
/// CustomerPaymentMethodId, FailureReason, CreatedAt/UpdatedAt/
/// AuthorizedAt/CapturedAt), same reasoning as
/// <c>CustomerAddressPersistenceModel</c>.
/// </summary>
public sealed class PaymentPersistenceModel
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid? CustomerPaymentMethodId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string? ProviderReference { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? AuthorizedAt { get; set; }

    public DateTimeOffset? CapturedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public int Version { get; set; }

    public ICollection<RefundPersistenceModel> Refunds { get; set; } = new List<RefundPersistenceModel>();
}
