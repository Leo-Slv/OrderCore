using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Modules.Messaging.Domain.Enums;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Mappers;

public static class FailedMessageMapper
{
    public static FailedMessage ToDomain(FailedMessagePersistenceModel model) =>
        FailedMessage.Rehydrate(
            model.Id,
            model.MessageId,
            model.Type,
            model.ContractVersion,
            model.Consumer,
            model.Body,
            model.TraceParent,
            model.TraceState,
            model.LastError,
            model.Attempts,
            model.FirstFailedAt,
            model.LastFailedAt,
            Enum.Parse<FailedMessageStatus>(model.Status),
            model.ResolvedAt,
            model.Version);

    public static FailedMessagePersistenceModel ToPersistence(FailedMessage domain) => new()
    {
        Id = domain.Id,
        MessageId = domain.MessageId,
        Type = domain.Type,
        ContractVersion = domain.ContractVersion,
        Consumer = domain.Consumer,
        Body = domain.Body,
        TraceParent = domain.TraceParent,
        TraceState = domain.TraceState,
        LastError = domain.LastError,
        Attempts = domain.Attempts,
        FirstFailedAt = domain.FirstFailedAt,
        LastFailedAt = domain.LastFailedAt,
        Status = domain.Status.ToString(),
        ResolvedAt = domain.ResolvedAt,
        Version = domain.Version,
    };

    public static void ApplyChanges(FailedMessage domain, FailedMessagePersistenceModel model)
    {
        model.LastError = domain.LastError;
        model.Attempts = domain.Attempts;
        model.LastFailedAt = domain.LastFailedAt;
        model.Status = domain.Status.ToString();
        model.ResolvedAt = domain.ResolvedAt;
        model.Version = domain.Version;
    }
}
