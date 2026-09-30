using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Application.Telemetry;
using OrderCore.Api.Modules.Notifications.Domain.Entities;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;

namespace OrderCore.Api.Modules.Notifications.Application.UseCases;

/// <summary>
/// Renders an e-mail from its template and queues it
/// (Docs/specs/identity/password-recovery.md, item 1): the caller never waits
/// for the provider nor fails because it is down — the dispatcher sends it
/// right after, retrying if needed. Logs the message id and template only.
/// </summary>
public sealed class QueueEmailUseCase
{
    private readonly IEmailMessageRepository _repository;
    private readonly IEmailTemplates _templates;
    private readonly NotificationsMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<QueueEmailUseCase> _logger;

    public QueueEmailUseCase(
        IEmailMessageRepository repository,
        IEmailTemplates templates,
        NotificationsMetrics metrics,
        TimeProvider timeProvider,
        ILogger<QueueEmailUseCase> logger)
    {
        _repository = repository;
        _templates = templates;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <returns>The queued message's id.</returns>
    public async Task<Guid> ExecuteAsync(
        string to, string template, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var rendered = _templates.Render(template, values);
        var message = EmailMessage.Queue(
            to, template, rendered.Subject, rendered.HtmlBody, rendered.TextBody, _timeProvider.GetUtcNow());

        await _repository.AddAsync(message, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _metrics.Email(template, "queued");
        _logger.LogInformation("Queued e-mail {EmailMessageId} ({EmailTemplate}).", message.Id, template);
        return message.Id;
    }
}
