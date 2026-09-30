using OrderCore.Api.Modules.Notifications.Domain.Enums;
using OrderCore.Api.Modules.Notifications.Domain.Policies;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Notifications.Domain.Entities;

/// <summary>
/// One e-mail, queued so the request that caused it never waits for the
/// provider, and kept until it is sent or given up on
/// (Docs/specs/identity/password-recovery.md). Once it is final its content
/// is erased (decision 7): the bodies may hold a single-use link, so the
/// record keeps only the masked recipient, the template and the outcome.
/// </summary>
public sealed class EmailMessage : AggregateRoot<Guid>
{
    public const int MaxErrorLength = 1000;

    /// <summary>The recipient — masked (<c>j***@example.com</c>) once the message is final.</summary>
    public string To { get; private set; } = string.Empty;

    /// <summary>Which template it was rendered from, e.g. <c>password-reset</c>.</summary>
    public string Template { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string HtmlBody { get; private set; } = string.Empty;

    public string TextBody { get; private set; } = string.Empty;

    public EmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>When it is due (again); meaningless once final.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Why the last attempt failed, with the recipient's address masked.</summary>
    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public bool ContentErased => Status != EmailStatus.Pending;

    private EmailMessage()
    {
    }

    private EmailMessage(Guid id) : base(id)
    {
    }

    public static EmailMessage Queue(string to, string template, string subject, string htmlBody, string textBody, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(to) || !IsAddress(to.Trim()))
        {
            throw new ArgumentException("The recipient must be an e-mail address.", nameof(to));
        }

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("The template is required.", nameof(template));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("The subject is required.", nameof(subject));
        }

        var message = new EmailMessage(Guid.NewGuid())
        {
            To = to.Trim(),
            Template = template,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            Status = EmailStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        };
        message.IncrementVersion();
        return message;
    }

    internal static EmailMessage Rehydrate(
        Guid id,
        string to,
        string template,
        string subject,
        string htmlBody,
        string textBody,
        EmailStatus status,
        int attempts,
        DateTimeOffset nextAttemptAt,
        string? lastError,
        DateTimeOffset createdAt,
        DateTimeOffset? sentAt,
        DateTimeOffset? failedAt,
        int version) =>
        new(id)
        {
            To = to,
            Template = template,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            Status = status,
            Attempts = attempts,
            NextAttemptAt = nextAttemptAt,
            LastError = lastError,
            CreatedAt = createdAt,
            SentAt = sentAt,
            FailedAt = failedAt,
            Version = version,
        };

    public void MarkSent(DateTimeOffset now)
    {
        EnsurePending();
        Attempts++;
        Status = EmailStatus.Sent;
        SentAt = now;
        LastError = null;
        EraseContent();
        IncrementVersion();
    }

    /// <summary>
    /// An attempt failed but may work later: due again after the policy's next
    /// delay, or — every attempt used — given up on.
    /// </summary>
    public void RecordFailure(string error, EmailRetryPolicy policy, DateTimeOffset now)
    {
        EnsurePending();
        Attempts++;
        LastError = Scrub(error);

        if (policy.DelayAfter(Attempts) is { } delay)
        {
            NextAttemptAt = now + delay;
        }
        else
        {
            GiveUp(now);
        }

        IncrementVersion();
    }

    /// <summary>The provider refused the message for good: failed at once, no retry.</summary>
    public void Reject(string error, DateTimeOffset now)
    {
        EnsurePending();
        Attempts++;
        LastError = Scrub(error);
        GiveUp(now);
        IncrementVersion();
    }

    /// <summary><c>jane@example.com</c> → <c>j***@example.com</c>.</summary>
    public static string Mask(string address)
    {
        var at = address.LastIndexOf('@');
        return at <= 0 ? "***" : $"{address[0]}***{address[at..]}";
    }

    private void GiveUp(DateTimeOffset now)
    {
        Status = EmailStatus.Failed;
        FailedAt = now;
        EraseContent();
    }

    private void EraseContent()
    {
        To = Mask(To);
        HtmlBody = string.Empty;
        TextBody = string.Empty;
    }

    private void EnsurePending()
    {
        if (Status != EmailStatus.Pending)
        {
            throw new DomainRuleViolationException(
                "invalid_email_state", $"E-mail '{Id}' was already {Status.ToString().ToLowerInvariant()}.");
        }
    }

    /// <summary>A provider's error may quote the address; the record never keeps it.</summary>
    private string Scrub(string error)
    {
        var scrubbed = string.IsNullOrWhiteSpace(error)
            ? "unknown error"
            : error.Replace(To, Mask(To), StringComparison.OrdinalIgnoreCase);
        return scrubbed.Length > MaxErrorLength ? scrubbed[..MaxErrorLength] : scrubbed;
    }

    private static bool IsAddress(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at == value.LastIndexOf('@') && at < value.Length - 1 && !value.Any(char.IsWhiteSpace);
    }
}
