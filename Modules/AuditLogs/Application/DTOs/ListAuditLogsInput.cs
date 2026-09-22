namespace OrderCore.Api.Modules.AuditLogs.Application.DTOs;

public sealed class ListAuditLogsInput
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 20;

    public const int MaximumPageSize = 100;

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;
}
