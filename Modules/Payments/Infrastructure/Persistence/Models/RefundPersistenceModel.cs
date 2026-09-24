namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;

/// <summary>
/// Expanded beyond 06-payments.md's abbreviated shape with
/// <see cref="Reason"/>/<see cref="ProcessedAt"/>, same reasoning as
/// <c>PaymentPersistenceModel</c>.
/// </summary>
public sealed class RefundPersistenceModel
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public PaymentPersistenceModel Payment { get; set; } = null!;
}
