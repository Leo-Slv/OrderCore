using OrderCore.Api.Modules.Messaging.Application.DTOs;
using OrderCore.Api.Modules.Messaging.Presentation.Requests;
using OrderCore.Api.Modules.Messaging.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Messaging.Presentation.Presenters;

public static class FailedMessagePresenter
{
    public static ListFailedMessagesInput ToInput(ListFailedMessagesRequest request) => new()
    {
        Page = request.Page,
        PageSize = request.PageSize,
        Status = request.Status,
    };

    public static PagedResponse<FailedMessageSummaryResponse> ToResponse(PagedResult<FailedMessageOutput> output) => new()
    {
        Items = output.Items.Select(ToSummary).ToList(),
        Page = output.Page,
        PageSize = output.PageSize,
        TotalItems = output.TotalItems,
        TotalPages = output.TotalPages,
    };

    public static FailedMessageDetailsResponse ToResponse(FailedMessageOutput output) => new()
    {
        Id = output.Id,
        MessageId = output.MessageId,
        Type = output.Type,
        ContractVersion = output.ContractVersion,
        Consumer = output.Consumer,
        Status = output.Status,
        Attempts = output.Attempts,
        LastError = output.LastError,
        FirstFailedAt = output.FirstFailedAt,
        LastFailedAt = output.LastFailedAt,
        ResolvedAt = output.ResolvedAt,
        TraceParent = output.TraceParent,
        Body = output.Body,
    };

    private static FailedMessageSummaryResponse ToSummary(FailedMessageOutput output) => new()
    {
        Id = output.Id,
        MessageId = output.MessageId,
        Type = output.Type,
        ContractVersion = output.ContractVersion,
        Consumer = output.Consumer,
        Status = output.Status,
        Attempts = output.Attempts,
        LastError = output.LastError,
        FirstFailedAt = output.FirstFailedAt,
        LastFailedAt = output.LastFailedAt,
        ResolvedAt = output.ResolvedAt,
    };
}
