namespace OrderCore.Api.Modules.Payments.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
