using Microsoft.Extensions.Options;
using MimeKit;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Senders;

/// <summary>
/// Stops the API at startup when e-mail would be queued but could never go
/// out: no sender address, or one that isn't an address; a Resend key that
/// isn't one; no SMTP host without Resend. Each failure names the setting;
/// the key is never echoed.
/// </summary>
public sealed class NotificationsOptionsValidator : IValidateOptions<NotificationsOptions>
{
    private readonly ResendOptions _resend;
    private readonly SmtpOptions _smtp;

    public NotificationsOptionsValidator(IOptions<ResendOptions> resend, IOptions<SmtpOptions> smtp)
    {
        _resend = resend.Value;
        _smtp = smtp.Value;
    }

    public ValidateOptionsResult Validate(string? name, NotificationsOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.From))
        {
            failures.Add("Notifications:From is required (the sender, e.g. \"OrderCore <no-reply@shop.example>\").");
        }
        else if (!MailboxAddress.TryParse(options.From, out _))
        {
            failures.Add("Notifications:From must be an e-mail address, optionally with a name (\"OrderCore <no-reply@shop.example>\").");
        }

        if (_resend.IsEnabled)
        {
            if (!_resend.ApiKey!.StartsWith("re_", StringComparison.Ordinal))
            {
                failures.Add("Notifications:Resend:ApiKey must be a Resend API key (re_…).");
            }
        }
        else if (string.IsNullOrWhiteSpace(_smtp.Host))
        {
            failures.Add("Notifications:Smtp:Host is required when Notifications:Resend:ApiKey isn't set.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
