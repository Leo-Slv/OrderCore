using OrderCore.Api.Modules.Notifications.Domain.Entities;
using OrderCore.Api.Modules.Notifications.Domain.Enums;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Mappers;

public static class EmailMessageMapper
{
    public static EmailMessage ToDomain(EmailMessagePersistenceModel model) =>
        EmailMessage.Rehydrate(
            model.Id,
            model.To,
            model.Template,
            model.Subject,
            model.HtmlBody,
            model.TextBody,
            Enum.Parse<EmailStatus>(model.Status),
            model.Attempts,
            model.NextAttemptAt,
            model.LastError,
            model.CreatedAt,
            model.SentAt,
            model.FailedAt,
            model.Version);

    public static EmailMessagePersistenceModel ToPersistence(EmailMessage message)
    {
        var model = new EmailMessagePersistenceModel
        {
            Id = message.Id,
            Template = message.Template,
            CreatedAt = message.CreatedAt,
        };
        ApplyChanges(message, model);
        return model;
    }

    public static void ApplyChanges(EmailMessage message, EmailMessagePersistenceModel model)
    {
        model.To = message.To;
        model.Subject = message.Subject;
        model.HtmlBody = message.HtmlBody;
        model.TextBody = message.TextBody;
        model.Status = message.Status.ToString();
        model.Attempts = message.Attempts;
        model.NextAttemptAt = message.NextAttemptAt;
        model.LastError = message.LastError;
        model.SentAt = message.SentAt;
        model.FailedAt = message.FailedAt;
        model.Version = message.Version;
    }
}
