using OrderCore.Api.Modules.Messaging.Application.DTOs;

namespace OrderCore.Api.Modules.Messaging.Presentation.Requests;

public sealed class ListFailedMessagesRequest
{
    public int Page { get; init; } = ListFailedMessagesInput.DefaultPage;

    public int PageSize { get; init; } = ListFailedMessagesInput.DefaultPageSize;

    /// <summary>Only messages in this status: <c>Pending</c>, <c>Replayed</c> or <c>Discarded</c>.</summary>
    public string? Status { get; init; }
}
