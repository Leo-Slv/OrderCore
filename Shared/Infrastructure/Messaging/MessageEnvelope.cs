using System.Text.Json;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// What travels on the wire: the event's identity and contract around its
/// payload, as JSON. The trace context travels as message headers
/// (<see cref="MessageHeaders"/>), not in the body.
/// </summary>
public sealed record MessageEnvelope(
    Guid MessageId,
    string Type,
    int Version,
    DateTimeOffset OccurredAt,
    Guid? CausationId,
    JsonElement Payload)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string SerializePayload(IntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);

    public static MessageEnvelope FromBytes(ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<MessageEnvelope>(body, JsonOptions)
            ?? throw new JsonException("Empty message envelope.");

    public static MessageEnvelope FromOutbox(OutboxMessage message)
    {
        using var payload = JsonDocument.Parse(message.PayloadJson);
        return new MessageEnvelope(
            message.Id, message.Type, message.Version, message.OccurredAt, message.CausationId, payload.RootElement.Clone());
    }

    public IntegrationEvent ToEvent(Type eventType) =>
        (IntegrationEvent?)Payload.Deserialize(eventType, JsonOptions)
            ?? throw new JsonException($"Message '{MessageId}' has an empty payload.");
}

/// <summary>Header names used on the broker.</summary>
public static class MessageHeaders
{
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";

    /// <summary>1 for the first delivery; set by the consumer host when it schedules a retry.</summary>
    public const string Attempt = "x-attempt";

    public const string FirstFailedAt = "x-first-failed-at";

    public const string LastError = "x-last-error";
}
