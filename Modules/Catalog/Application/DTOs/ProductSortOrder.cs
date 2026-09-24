namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

public enum ProductSortOrder
{
    /// <summary>Alphabetical. The default, and the order the listing always used.</summary>
    Name,
    PriceAsc,
    PriceDesc,

    /// <summary>Most recently published first. Never-published products come last.</summary>
    Newest,
}
