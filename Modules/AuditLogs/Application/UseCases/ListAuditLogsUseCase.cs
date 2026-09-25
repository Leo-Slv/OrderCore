using OrderCore.Api.Modules.AuditLogs.Application.DTOs;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.AuditLogs.Application.UseCases;

public sealed class ListAuditLogsUseCase
{
    private readonly IAuditLogRepository _auditLogs;

    public ListAuditLogsUseCase(IAuditLogRepository auditLogs)
    {
        _auditLogs = auditLogs;
    }

    public async Task<PagedResult<AuditLogOutput>> ExecuteAsync(ListAuditLogsInput input, CancellationToken cancellationToken)
    {
        if (input.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Page must be greater than or equal to 1.");
        }

        if (input.PageSize is < 1 or > ListAuditLogsInput.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"PageSize must be between 1 and {ListAuditLogsInput.MaximumPageSize}.");
        }

        var filter = new AuditLogFilter(NullIfBlank(input.EntityName), input.EntityId, input.UserId, NullIfBlank(input.Action));
        var (items, totalCount) = await _auditLogs.ListPagedAsync(filter, input.Page, input.PageSize, cancellationToken);

        return new PagedResult<AuditLogOutput>
        {
            Items = items.Select(AuditLogOutput.FromAuditLog).ToList(),
            Page = input.Page,
            PageSize = input.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)input.PageSize),
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
