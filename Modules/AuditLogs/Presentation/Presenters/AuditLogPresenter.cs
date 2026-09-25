using OrderCore.Api.Modules.AuditLogs.Application.DTOs;
using OrderCore.Api.Modules.AuditLogs.Presentation.Requests;
using OrderCore.Api.Modules.AuditLogs.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.AuditLogs.Presentation.Presenters;

public static class AuditLogPresenter
{
    public static ListAuditLogsInput ToInput(ListAuditLogsRequest request) => new()
    {
        Page = request.Page,
        PageSize = request.PageSize,
        EntityName = request.EntityName,
        EntityId = request.EntityId,
        UserId = request.UserId,
        Action = request.Action,
    };

    public static AuditLogResponse ToResponse(AuditLogOutput output) => new()
    {
        Id = output.Id,
        UserId = output.UserId,
        Action = output.Action,
        EntityName = output.EntityName,
        EntityId = output.EntityId,
        Metadata = output.Metadata,
        CreatedAt = output.CreatedAt,
    };

    public static PagedResponse<AuditLogResponse> ToResponse(PagedResult<AuditLogOutput> output) => new()
    {
        Items = output.Items.Select(ToResponse).ToList(),
        Page = output.Page,
        PageSize = output.PageSize,
        TotalItems = output.TotalItems,
        TotalPages = output.TotalPages,
    };
}
