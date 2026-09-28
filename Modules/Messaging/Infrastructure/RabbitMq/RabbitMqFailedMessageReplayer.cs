using System.Text;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;

/// <summary>
/// Publishes the envelope, exactly as it was received, straight to the
/// consumer's queue through the default exchange (routing key = queue name),
/// so only that consumer gets it. It arrives as attempt 1 — a fresh round of
/// five attempts — carrying its original trace context, and the call returns
/// only once the broker confirmed it; a queue that no longer exists makes the
/// publish fail instead of dropping the message.
/// </summary>
public sealed class RabbitMqFailedMessageReplayer : IFailedMessageReplayer
{
    private readonly RabbitMqConnection _connection;
    private readonly MessagingOptions _messaging;

    public RabbitMqFailedMessageReplayer(RabbitMqConnection connection, IOptions<MessagingOptions> messaging)
    {
        _connection = connection;
        _messaging = messaging.Value;
    }

    public async Task ReplayAsync(FailedMessage failedMessage, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, object?> { [MessageHeaders.Attempt] = 1 };
        if (failedMessage.TraceParent is not null)
        {
            headers[MessageHeaders.TraceParent] = Encoding.UTF8.GetBytes(failedMessage.TraceParent);
        }

        if (failedMessage.TraceState is not null)
        {
            headers[MessageHeaders.TraceState] = Encoding.UTF8.GetBytes(failedMessage.TraceState);
        }

        var properties = new BasicProperties
        {
            MessageId = failedMessage.MessageId.ToString(),
            Type = failedMessage.Type,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = headers,
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_messaging.PublishTimeout);

        await using var channel = await _connection.CreatePublishingChannelAsync(timeout.Token);
        await channel.BasicPublishAsync(
            string.Empty,
            failedMessage.Consumer,
            mandatory: true,
            properties,
            Encoding.UTF8.GetBytes(failedMessage.Body),
            timeout.Token);
    }
}
