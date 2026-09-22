namespace OrderCore.Api.Modules.AuditLogs.Presentation.Responses;

public sealed class AuditLogResponse
{
    public required Guid Id { get; init; }

    public Guid? UserId { get; init; }

    public required string Action { get; init; }

    public required string EntityName { get; init; }

    public Guid? EntityId { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public required DateTimeOffset CreatedAt { get; init; }
}
