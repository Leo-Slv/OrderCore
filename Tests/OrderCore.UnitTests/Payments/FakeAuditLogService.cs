using OrderCore.Api.Modules.AuditLogs.Application.Services;

namespace OrderCore.UnitTests.Payments;

public sealed class FakeAuditLogService : IAuditLogService
{
    /// <summary>Every action recorded, in order.</summary>
    public List<(string Action, IReadOnlyDictionary<string, string?>? Metadata)> Entries { get; } = [];

    public Task RecordAsync(
        string action,
        string entityName,
        Guid? entityId,
        IReadOnlyDictionary<string, string?>? metadata,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        Entries.Add((action, metadata));
        return Task.CompletedTask;
    }
}
