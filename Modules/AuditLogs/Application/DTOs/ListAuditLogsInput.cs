namespace OrderCore.Api.Modules.AuditLogs.Application.DTOs;

public sealed class ListAuditLogsInput
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 20;

    public const int MaximumPageSize = 100;

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>E.g. <c>Order</c>, <c>Payment</c>, <c>Product</c>.</summary>
    public string? EntityName { get; init; }

    public Guid? EntityId { get; init; }

    /// <summary>The actor: the signed-in user who caused the entry.</summary>
    public Guid? UserId { get; init; }

    /// <summary>One of <c>AuditLogActionNames</c>.</summary>
    public string? Action { get; init; }
}
