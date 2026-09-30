namespace OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Models;

public sealed class EmailMessagePersistenceModel
{
    public Guid Id { get; set; }

    public string To { get; set; } = string.Empty;

    public string Template { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public string TextBody { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? FailedAt { get; set; }

    public int Version { get; set; }
}
