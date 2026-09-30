using OrderCore.Api.Modules.Notifications.Application.Telemetry;
using OrderCore.Api.Modules.Notifications.Domain.Enums;
using OrderCore.Api.Modules.Notifications.Domain.Policies;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;

namespace OrderCore.Api.Modules.Notifications.Application.UseCases;

/// <summary>
/// Sends the queued e-mails that are due, one at a time: the outcome is
/// recorded on the message — sent, due again after the retry policy's next
/// delay, or given up on — and its content erased once final (decision 7).
/// Delivery is at least once: if recording a sent message fails, it is sent
/// again on the next pass (Resend dedupes by the message id for 24 hours).
/// </summary>
public sealed class SendEmailUseCase
{
    private readonly IEmailMessageRepository _repository;
    private readonly IEmailSender _sender;
    private readonly EmailRetryPolicy _retryPolicy;
    private readonly NotificationsMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SendEmailUseCase> _logger;

    public SendEmailUseCase(
        IEmailMessageRepository repository,
        IEmailSender sender,
        EmailRetryPolicy retryPolicy,
        NotificationsMetrics metrics,
        TimeProvider timeProvider,
        ILogger<SendEmailUseCase> logger)
    {
        _repository = repository;
        _sender = sender;
        _retryPolicy = retryPolicy;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<IReadOnlyList<Guid>> FindDueAsync(int limit, CancellationToken cancellationToken) =>
        _repository.ListDueAsync(_timeProvider.GetUtcNow(), limit, cancellationToken);

    /// <summary>Sends one message if it is still pending and due; anything else is left alone.</summary>
    public async Task SendAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _repository.GetByIdAsync(messageId, cancellationToken);
        if (message is null || message.Status != EmailStatus.Pending || message.NextAttemptAt > _timeProvider.GetUtcNow())
        {
            return;
        }

        var result = await CallProviderAsync(
            new OutgoingEmail(message.Id, message.To, message.Subject, message.HtmlBody, message.TextBody), cancellationToken);
        var now = _timeProvider.GetUtcNow();

        string outcome;
        switch (result.Outcome)
        {
            case EmailSendOutcome.Sent:
                message.MarkSent(now);
                outcome = "sent";
                break;
            case EmailSendOutcome.Rejected:
                message.Reject(result.Error ?? "rejected", now);
                outcome = "failed";
                break;
            default:
                message.RecordFailure(result.Error ?? "unknown error", _retryPolicy, now);
                outcome = message.Status == EmailStatus.Failed ? "failed" : "retried";
                break;
        }

        await _repository.SaveChangesAsync(cancellationToken);
        _metrics.Email(message.Template, outcome);

        switch (outcome)
        {
            case "sent":
                _logger.LogInformation("Sent e-mail {EmailMessageId} ({EmailTemplate}).", message.Id, message.Template);
                break;
            case "retried":
                _logger.LogWarning(
                    "Sending e-mail {EmailMessageId} ({EmailTemplate}) failed on attempt {Attempt}; trying again at {NextAttemptAt}.",
                    message.Id, message.Template, message.Attempts, message.NextAttemptAt);
                break;
            default:
                _logger.LogError(
                    "Gave up on e-mail {EmailMessageId} ({EmailTemplate}) after {Attempts} attempt(s): {EmailError}",
                    message.Id, message.Template, message.Attempts, message.LastError);
                break;
        }
    }

    private async Task<EmailSendResult> CallProviderAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var started = _timeProvider.GetTimestamp();
        EmailSendResult result;
        try
        {
            result = await _sender.SendAsync(email, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Kept on the message (scrubbed of the address), not logged: a
            // provider's error may quote the recipient.
            result = EmailSendResult.Transient($"{exception.GetType().Name}: {exception.Message}");
        }

        var outcome = result.Outcome switch
        {
            EmailSendOutcome.Sent => "sent",
            EmailSendOutcome.Rejected => "rejected",
            _ => "error",
        };
        _metrics.ProviderCalled(_sender.Name, outcome, _timeProvider.GetElapsedTime(started));
        return result;
    }
}
