using MailKit.Security;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Senders;

/// <summary>Section <c>Notifications</c>.</summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>The sender, e.g. <c>OrderCore &lt;no-reply@shop.example&gt;</c> — with Resend, on a verified domain.</summary>
    public string? From { get; set; }
}

/// <summary>
/// Section <c>Notifications:Resend</c>. E-mail goes out through Resend's API
/// when <see cref="ApiKey"/> is set (spec decision 1) — from the environment
/// or user-secrets only, never committed; through SMTP otherwise.
/// </summary>
public sealed class ResendOptions
{
    public const string SectionName = "Notifications:Resend";

    public string? ApiKey { get; set; }

    /// <summary>Resend's API; only changed by tests.</summary>
    public Uri ApiBase { get; set; } = new("https://api.resend.com/");

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Section <c>Notifications:Smtp</c>: the SMTP server used when Resend isn't
/// configured — Mailpit in docker compose (<c>localhost:1025</c>, no
/// credentials, web inbox on port 8025).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Notifications:Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 1025;

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary><c>Auto</c> uses TLS when the server offers it; Mailpit offers none.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
}
