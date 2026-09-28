using OrderCore.Api.Modules.Messaging.Domain.Entities;

namespace OrderCore.Api.Modules.Messaging.Application.DTOs;

public sealed record FailedMessageOutput(
    Guid Id,
    Guid MessageId,
    string Type,
    int ContractVersion,
    string Consumer,
    string Status,
    int Attempts,
    string LastError,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset LastFailedAt,
    DateTimeOffset? ResolvedAt,
    string? TraceParent,
    string Body)
{
    public static FailedMessageOutput From(FailedMessage message) => new(
        message.Id,
        message.MessageId,
        message.Type,
        message.ContractVersion,
        message.Consumer,
        message.Status.ToString(),
        message.Attempts,
        message.LastError,
        message.FirstFailedAt,
        message.LastFailedAt,
        message.ResolvedAt,
        message.TraceParent,
        message.Body);
}
