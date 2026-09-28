using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Messaging.Application.UseCases;

/// <summary>
/// Gives up on a pending failed message for good: it is never sent again,
/// and stays listed as <c>Discarded</c> for the record.
/// </summary>
public sealed class DiscardFailedMessageUseCase
{
    private readonly IFailedMessageRepository _failedMessages;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public DiscardFailedMessageUseCase(IFailedMessageRepository failedMessages, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _failedMessages = failedMessages;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<FailedMessageOutput> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await _failedMessages.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("failed_message_not_found", $"Failed message '{id}' was not found.");

        message.Discard(_timeProvider.GetUtcNow());
        await _failedMessages.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.FailedMessageDiscarded,
            "FailedMessage",
            message.Id,
            new Dictionary<string, string?> { ["type"] = message.Type, ["consumer"] = message.Consumer },
            userId: null,
            cancellationToken);

        return FailedMessageOutput.From(message);
    }
}
