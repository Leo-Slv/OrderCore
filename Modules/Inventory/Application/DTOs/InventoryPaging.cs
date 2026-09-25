using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

/// <summary>
/// Page bounds shared by the Inventory listings, validated the same way
/// <c>ListAuditLogsUseCase</c> validates them.
/// </summary>
public static class InventoryPaging
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public static void Validate(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than or equal to 1.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"PageSize must be between 1 and {MaximumPageSize}.");
        }
    }

    public static PagedResult<T> ToPagedResult<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalItems = totalCount,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
    };
}
