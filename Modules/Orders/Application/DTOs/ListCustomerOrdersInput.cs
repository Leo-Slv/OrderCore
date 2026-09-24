namespace OrderCore.Api.Modules.Orders.Application.DTOs;

public sealed class ListCustomerOrdersInput
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public Guid CustomerId { get; init; }

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;
}
