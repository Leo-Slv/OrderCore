namespace OrderCore.Api.Modules.Notifications.Domain.Repositories;

/// <summary>What is handed to the provider: the message id doubles as its idempotency key.</summary>
public sealed record OutgoingEmail(Guid MessageId, string To, string Subject, string HtmlBody, string TextBody);

public enum EmailSendOutcome
{
    Sent,

    /// <summary>The provider refused this message for good (e.g. an invalid address): trying again won't help.</summary>
    Rejected,

    /// <summary>It may work later (provider down, rate limited, timeout).</summary>
    TransientFailure,
}

/// <summary><see cref="Error"/> is the provider's short explanation, for the record; never shown to anyone.</summary>
public sealed record EmailSendResult(EmailSendOutcome Outcome, string? Error = null)
{
    public static readonly EmailSendResult Sent = new(EmailSendOutcome.Sent);

    public static EmailSendResult Rejected(string error) => new(EmailSendOutcome.Rejected, error);

    public static EmailSendResult Transient(string error) => new(EmailSendOutcome.TransientFailure, error);
}

/// <summary>
/// The abstraction Notifications sends e-mail through, the way Payments
/// talks to a provider through <c>IPaymentProvider</c>: Resend when its key
/// is configured, SMTP (Mailpit locally) otherwise. A sender answers with a
/// result; an exception it lets escape counts as a transient failure.
/// </summary>
public interface IEmailSender
{
    /// <summary>For logs and metrics: <c>resend</c> or <c>smtp</c>.</summary>
    string Name { get; }

    Task<EmailSendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}
