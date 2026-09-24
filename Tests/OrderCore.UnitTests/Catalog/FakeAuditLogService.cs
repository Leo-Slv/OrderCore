using OrderCore.Api.Modules.AuditLogs.Application.Services;

namespace OrderCore.UnitTests.Catalog;

public sealed class FakeAuditLogService : IAuditLogService
{
    public Task RecordAsync(
        string action,
        string entityName,
        Guid? entityId,
        IReadOnlyDictionary<string, string?>? metadata,
        Guid? userId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
