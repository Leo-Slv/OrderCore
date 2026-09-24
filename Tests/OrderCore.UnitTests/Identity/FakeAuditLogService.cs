using OrderCore.Api.Modules.AuditLogs.Application.Services;

namespace OrderCore.UnitTests.Identity;

internal sealed class FakeAuditLogService : IAuditLogService
{
    public List<string> Actions { get; } = new();

    public Task RecordAsync(
        string action,
        string entityName,
        Guid? entityId,
        IReadOnlyDictionary<string, string?>? metadata,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }
}
