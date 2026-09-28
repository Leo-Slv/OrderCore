using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Application.DTOs;
using OrderCore.Api.Modules.Messaging.Domain.Enums;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Messaging.Application.UseCases;

/// <summary>The messages consumers gave up on, most recent failure first, optionally by status.</summary>
public sealed class ListFailedMessagesUseCase
{
    private readonly IFailedMessageRepository _failedMessages;

    public ListFailedMessagesUseCase(IFailedMessageRepository failedMessages)
    {
        _failedMessages = failedMessages;
    }

    public async Task<PagedResult<FailedMessageOutput>> ExecuteAsync(ListFailedMessagesInput input, CancellationToken cancellationToken)
    {
        if (input.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Page must be greater than or equal to 1.");
        }

        if (input.PageSize is < 1 or > ListFailedMessagesInput.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input), $"PageSize must be between 1 and {ListFailedMessagesInput.MaximumPageSize}.");
        }

        FailedMessageStatus? status = null;
        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            status = Enum.TryParse<FailedMessageStatus>(input.Status.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new ArgumentException($"Unknown status '{input.Status}'.", nameof(input));
        }

        var (items, totalCount) = await _failedMessages.ListAsync(status, input.Page, input.PageSize, cancellationToken);

        return new PagedResult<FailedMessageOutput>
        {
            Items = items.Select(FailedMessageOutput.From).ToList(),
            Page = input.Page,
            PageSize = input.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)input.PageSize),
        };
    }
}
