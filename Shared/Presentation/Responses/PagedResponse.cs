namespace OrderCore.Api.Shared.Presentation.Responses;

/// <summary>
/// HTTP-facing counterpart of <see cref="Shared.Application.DTOs.PagedResult{T}"/>.
/// Kept as a separate type instead of reusing PagedResult directly so the
/// wire contract (Presentation) can evolve independently from the
/// Application-layer DTO, the same separation used for single-item
/// responses (section 5.1 — Presenters translate Application DTOs into
/// Presentation responses).
/// </summary>
public sealed class PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalItems { get; init; }

    public required int TotalPages { get; init; }
}
