using OrderCore.Api.Modules.AuditLogs.Application.Services;

namespace OrderCore.UnitTests.Orders;

internal sealed class FakeAuditLogService : IAuditLogService
{
    public List<(string Action, IReadOnlyDictionary<string, string?>? Metadata)> Entries { get; } = new();

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
