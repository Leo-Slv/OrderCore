namespace OrderCore.Api.Shared.Application.DTOs;

/// <summary>
/// Generic paged result for list use cases. Lives under Shared (not inside
/// a single module) because pagination is a technical, cross-cutting
/// concern rather than business logic belonging to one module — the first
/// consumer is <c>ListAuditLogsUseCase</c>, but any module listing
/// collections can reuse it instead of inventing its own shape.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalItems { get; init; }

    public required int TotalPages { get; init; }
}
