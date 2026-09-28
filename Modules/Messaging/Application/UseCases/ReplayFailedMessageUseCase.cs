using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Messaging.Application.UseCases;

/// <summary>
/// Sends a pending failed message back to its consumer, for when the cause
/// is gone (a fix deployed, a dependency back up). The message is checked
/// first and marked <c>Replayed</c> only after it was sent: if saving then
/// fails, replaying again is harmless, because the consumer's inbox turns a
/// second delivery of a message it handled into a no-op. If it fails again,
/// it goes through its five attempts and lands here as a new entry.
/// </summary>
public sealed class ReplayFailedMessageUseCase
{
    private readonly IFailedMessageRepository _failedMessages;
    private readonly IFailedMessageReplayer _replayer;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public ReplayFailedMessageUseCase(
        IFailedMessageRepository failedMessages, IFailedMessageReplayer replayer, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _failedMessages = failedMessages;
        _replayer = replayer;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<FailedMessageOutput> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await _failedMessages.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("failed_message_not_found", $"Failed message '{id}' was not found.");

        message.EnsurePending();
        await _replayer.ReplayAsync(message, cancellationToken);

        message.MarkReplayed(_timeProvider.GetUtcNow());
        await _failedMessages.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.FailedMessageReplayed,
            "FailedMessage",
            message.Id,
            new Dictionary<string, string?> { ["type"] = message.Type, ["consumer"] = message.Consumer },
            userId: null,
            cancellationToken);

        return FailedMessageOutput.From(message);
    }
}
