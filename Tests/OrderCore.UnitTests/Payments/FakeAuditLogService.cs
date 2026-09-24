using OrderCore.Api.Modules.AuditLogs.Application.Services;

namespace OrderCore.UnitTests.Payments;

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
