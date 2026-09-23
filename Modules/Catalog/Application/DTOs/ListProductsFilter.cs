namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

public sealed class ListProductsFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;

    public Guid? CategoryId { get; init; }

    public bool? Active { get; init; }

    public string? SearchTerm { get; init; }

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;
}
