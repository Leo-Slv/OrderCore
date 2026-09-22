using OrderCore.Api.Modules.AuditLogs.Application.DTOs;

namespace OrderCore.Api.Modules.AuditLogs.Presentation.Requests;

public sealed class ListAuditLogsRequest
{
    public int Page { get; init; } = ListAuditLogsInput.DefaultPage;

    public int PageSize { get; init; } = ListAuditLogsInput.DefaultPageSize;
}
