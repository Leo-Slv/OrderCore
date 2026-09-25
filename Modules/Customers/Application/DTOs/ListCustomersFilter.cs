namespace OrderCore.Api.Modules.Customers.Application.DTOs;

/// <summary>The backoffice customer list, newest first.</summary>
public sealed class ListCustomersFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    /// <summary>Matches the name or the e-mail, case-insensitively.</summary>
    public string? SearchTerm { get; init; }

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;
}
