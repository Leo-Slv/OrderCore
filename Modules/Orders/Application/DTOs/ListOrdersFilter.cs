using OrderCore.Api.Modules.Orders.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// The backoffice order list, newest first. Every criterion is optional;
/// the created range is <c>[CreatedFrom, CreatedTo)</c>.
/// </summary>
public sealed class ListOrdersFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public OrderStatus? Status { get; init; }

    public Guid? CustomerId { get; init; }

    public DateTimeOffset? CreatedFrom { get; init; }

    public DateTimeOffset? CreatedTo { get; init; }

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;
}
