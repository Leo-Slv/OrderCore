using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Messaging.Application.UseCases;

/// <summary>One failed message in full, including the envelope as it was received.</summary>
public sealed class GetFailedMessageUseCase
{
    private readonly IFailedMessageRepository _failedMessages;

    public GetFailedMessageUseCase(IFailedMessageRepository failedMessages)
    {
        _failedMessages = failedMessages;
    }

    public async Task<FailedMessageOutput> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await _failedMessages.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("failed_message_not_found", $"Failed message '{id}' was not found.");

        return FailedMessageOutput.From(message);
    }
}
