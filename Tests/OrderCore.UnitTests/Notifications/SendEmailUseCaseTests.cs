using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OrderCore.Api.Modules.Notifications.Application.Telemetry;
using OrderCore.Api.Modules.Notifications.Application.UseCases;
using OrderCore.Api.Modules.Notifications.Domain.Entities;
using OrderCore.Api.Modules.Notifications.Domain.Enums;
using OrderCore.Api.Modules.Notifications.Domain.Policies;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;
using OrderCore.Api.Modules.Notifications.Infrastructure.Templates;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>
/// Queuing never waits for the provider; sending records what the provider
/// answered — sent, retried later, or given up on.
/// </summary>
public sealed class SendEmailUseCaseTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeEmailMessageRepository _repository = new();
    private readonly StubEmailSender _sender = new();
    private readonly MetricsProbe _metrics = new();

    public void Dispose() => _metrics.Dispose();

    private SendEmailUseCase SendUseCase() => new(
        _repository, _sender, EmailRetryPolicy.Default, new NotificationsMetrics(_metrics.Factory), _time, NullLogger<SendEmailUseCase>.Instance);

    private async Task<Guid> QueueAsync()
    {
        var queue = new QueueEmailUseCase(
            _repository, new EmbeddedEmailTemplates(), new NotificationsMetrics(_metrics.Factory), _time, NullLogger<QueueEmailUseCase>.Instance);
        return await queue.ExecuteAsync(
            "jane@example.com",
            "password-reset",
            new Dictionary<string, string> { ["greeting"] = "Olá, Jane.", ["link"] = "https://shop.example/r?token=t", ["validFor"] = "30 minutos" },
            CancellationToken.None);
    }

    [Fact]
    public async Task Queuing_renders_and_saves_without_calling_the_provider()
    {
        var id = await QueueAsync();

        _sender.Calls.Should().BeEmpty();
        var message = _repository.Messages[id];
        message.Status.Should().Be(EmailStatus.Pending);
        message.Subject.Should().Be("Redefinição de senha");
        message.TextBody.Should().Contain("https://shop.example/r?token=t");
        _metrics.Of("ordercore.notifications.emails").Should().ContainSingle(m => (string)m.Tags["ordercore.outcome"]! == "queued");
    }

    [Fact]
    public async Task A_due_email_is_sent_with_its_id_and_its_content_erased()
    {
        var id = await QueueAsync();
        var useCase = SendUseCase();

        (await useCase.FindDueAsync(10, CancellationToken.None)).Should().Equal(id);
        await useCase.SendAsync(id, CancellationToken.None);

        var sent = _sender.Calls.Should().ContainSingle().Subject;
        sent.MessageId.Should().Be(id);
        sent.To.Should().Be("jane@example.com");
        sent.HtmlBody.Should().Contain("https://shop.example/r?token=t");
        var message = _repository.Messages[id];
        message.Status.Should().Be(EmailStatus.Sent);
        message.ContentErased.Should().BeTrue();
        (await useCase.FindDueAsync(10, CancellationToken.None)).Should().BeEmpty();
        _metrics.Of("ordercore.notifications.emails").Should().Contain(m =>
            (string)m.Tags["ordercore.outcome"]! == "sent" && (string)m.Tags["ordercore.email_template"]! == "password-reset");
    }

    [Fact]
    public async Task A_transient_failure_waits_for_the_next_attempt()
    {
        var id = await QueueAsync();
        _sender.Next = EmailSendResult.Transient("429 rate_limit_exceeded");
        var useCase = SendUseCase();

        await useCase.SendAsync(id, CancellationToken.None);

        var message = _repository.Messages[id];
        message.Status.Should().Be(EmailStatus.Pending);
        message.NextAttemptAt.Should().Be(_time.GetUtcNow().AddSeconds(30));
        (await useCase.FindDueAsync(10, CancellationToken.None)).Should().BeEmpty("it isn't due yet");

        await useCase.SendAsync(id, CancellationToken.None);
        _sender.Calls.Should().HaveCount(1, "a message not yet due is left alone");

        _time.Advance(TimeSpan.FromSeconds(30));
        _sender.Next = EmailSendResult.Sent;
        await useCase.SendAsync(id, CancellationToken.None);
        message.Status.Should().Be(EmailStatus.Sent);
        message.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task An_exception_from_the_provider_counts_as_a_transient_failure()
    {
        var id = await QueueAsync();
        _sender.Throw = new HttpRequestException("Connection refused (jane@example.com)");

        await SendUseCase().SendAsync(id, CancellationToken.None);

        var message = _repository.Messages[id];
        message.Status.Should().Be(EmailStatus.Pending);
        message.LastError.Should().Be("HttpRequestException: Connection refused (j***@example.com)");
        _metrics.Of("ordercore.notifications.send.duration").Should().ContainSingle(m => (string)m.Tags["ordercore.outcome"]! == "error");
    }

    [Fact]
    public async Task A_refusal_fails_the_email_at_once()
    {
        var id = await QueueAsync();
        _sender.Next = EmailSendResult.Rejected("422 validation_error");

        await SendUseCase().SendAsync(id, CancellationToken.None);

        var message = _repository.Messages[id];
        message.Status.Should().Be(EmailStatus.Failed);
        message.ContentErased.Should().BeTrue();
        _metrics.Of("ordercore.notifications.emails").Should().Contain(m => (string)m.Tags["ordercore.outcome"]! == "failed");
    }

    [Fact]
    public async Task Old_finished_records_are_purged_and_pending_ones_kept()
    {
        var oldSent = await QueueAsync();
        await SendUseCase().SendAsync(oldSent, CancellationToken.None);
        _time.Advance(TimeSpan.FromDays(91));
        var pending = await QueueAsync();

        var removed = await new PurgeFinishedEmailsUseCase(_repository, _time).ExecuteAsync(TimeSpan.FromDays(90), 100, CancellationToken.None);

        removed.Should().Be(1);
        _repository.Messages.Keys.Should().Equal(pending);
    }

    private sealed class StubEmailSender : IEmailSender
    {
        public List<OutgoingEmail> Calls { get; } = [];

        public EmailSendResult Next { get; set; } = EmailSendResult.Sent;

        public Exception? Throw { get; set; }

        public string Name => "stub";

        public Task<EmailSendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
        {
            Calls.Add(email);
            return Throw is null ? Task.FromResult(Next) : Task.FromException<EmailSendResult>(Throw);
        }
    }

    private sealed class FakeEmailMessageRepository : IEmailMessageRepository
    {
        public Dictionary<Guid, EmailMessage> Messages { get; } = [];

        public Task AddAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages[message.Id] = message;
            return Task.CompletedTask;
        }

        public Task<EmailMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Messages.GetValueOrDefault(id));

        public Task<IReadOnlyList<Guid>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(Messages.Values
                .Where(m => m.Status == EmailStatus.Pending && m.NextAttemptAt <= now)
                .OrderBy(m => m.NextAttemptAt)
                .Select(m => m.Id)
                .Take(limit)
                .ToList());

        public Task<int> DeleteFinishedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken)
        {
            var old = Messages.Values.Where(m => (m.SentAt ?? m.FailedAt) < cutoff).Take(limit).Select(m => m.Id).ToList();
            old.ForEach(id => Messages.Remove(id));
            return Task.FromResult(old.Count);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
