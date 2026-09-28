namespace OrderCore.Api.Modules.Messaging.Application.DTOs;

public sealed class ListFailedMessagesInput
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 20;

    public const int MaximumPageSize = 100;

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary><c>Pending</c>, <c>Replayed</c> or <c>Discarded</c>; every status when empty.</summary>
    public string? Status { get; init; }
}
