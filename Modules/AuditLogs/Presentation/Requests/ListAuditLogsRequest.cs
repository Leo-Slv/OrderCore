using OrderCore.Api.Modules.AuditLogs.Application.DTOs;

namespace OrderCore.Api.Modules.AuditLogs.Presentation.Requests;

public sealed class ListAuditLogsRequest
{
    public int Page { get; init; } = ListAuditLogsInput.DefaultPage;

    public int PageSize { get; init; } = ListAuditLogsInput.DefaultPageSize;

    /// <summary>Only entries about this kind of entity, e.g. <c>Order</c>.</summary>
    public string? EntityName { get; init; }

    /// <summary>Only entries about this entity; with <see cref="EntityName"/>, its timeline.</summary>
    public Guid? EntityId { get; init; }

    /// <summary>Only entries caused by this user.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Only entries of this action, e.g. <c>OrderCancelled</c>.</summary>
    public string? Action { get; init; }
}
