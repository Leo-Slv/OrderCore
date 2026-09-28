namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Models;

public sealed class FailedMessagePersistenceModel
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public string Type { get; set; } = string.Empty;

    public int ContractVersion { get; set; }

    public string Consumer { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? TraceParent { get; set; }

    public string? TraceState { get; set; }

    public string LastError { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public DateTimeOffset FirstFailedAt { get; set; }

    public DateTimeOffset LastFailedAt { get; set; }

    public string Status { get; set; } = string.Empty;

    public int Version { get; set; }
}
