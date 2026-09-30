using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Senders.Smtp;

/// <summary>
/// Sends through an SMTP server (MailKit) — Mailpit locally and in tests, so
/// development never sends real e-mail. A permanent refusal of the recipient
/// (a <c>5xx</c> reply) fails the message at once; a connection problem or a
/// temporary <c>4xx</c> is retried. One connection per message: the volume is
/// small and it keeps a broken connection from affecting the next one.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _smtp;
    private readonly NotificationsOptions _notifications;

    public SmtpEmailSender(IOptions<SmtpOptions> smtp, IOptions<NotificationsOptions> notifications)
    {
        _smtp = smtp.Value;
        _notifications = notifications.Value;
    }

    public string Name => "smtp";

    public async Task<EmailSendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        if (!MailboxAddress.TryParse(email.To, out var recipient))
        {
            return EmailSendResult.Rejected("The recipient isn't a valid address.");
        }

        var from = MailboxAddress.Parse(_notifications.From!);
        var message = new MimeMessage
        {
            Subject = email.Subject,
            Body = new BodyBuilder { HtmlBody = email.HtmlBody, TextBody = email.TextBody }.ToMessageBody(),
            MessageId = $"{email.MessageId:N}@{from.Domain}",
        };
        message.From.Add(from);
        message.To.Add(recipient);

        using var client = new SmtpClient { Timeout = (int)_smtp.Timeout.TotalMilliseconds };
        try
        {
            await client.ConnectAsync(_smtp.Host!, _smtp.Port, _smtp.Security, cancellationToken);
            if (!string.IsNullOrEmpty(_smtp.Username))
            {
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
            return EmailSendResult.Sent;
        }
        catch (SmtpCommandException exception) when ((int)exception.StatusCode >= 500
            && exception.ErrorCode == SmtpErrorCode.RecipientNotAccepted)
        {
            return EmailSendResult.Rejected($"SMTP {(int)exception.StatusCode}: {exception.Message}");
        }
        catch (SmtpCommandException exception)
        {
            return EmailSendResult.Transient($"SMTP {(int)exception.StatusCode}: {exception.Message}");
        }
    }
}
