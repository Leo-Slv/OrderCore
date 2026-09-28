using OrderCore.Api.Shared.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;

/// <summary>
/// The broker layout, declared at startup from what the modules registered:
/// <list type="bullet">
/// <item>one durable topic exchange every event is published to, routed by
/// <c>{contract}.v{version}</c>;</item>
/// <item>one durable queue per consumer, bound to the events it handles —
/// each consumer gets its own copy of every event it subscribes to;</item>
/// <item>per consumer, one waiting queue per retry step
/// (<c>{queue}.retry.{attempt}</c>): a message sits there for that step's
/// delay (queue TTL) and is then dead-lettered back to the consumer's
/// queue — delayed retries without a broker plugin.</item>
/// </list>
/// Declaring is idempotent; a queue declared again with different arguments
/// (e.g. another delay) is refused by RabbitMQ, so a changed schedule needs
/// its waiting queues deleted first.
/// </summary>
public static class RabbitMqTopology
{
    public static string RetryQueue(string queue, int failedAttempt) => $"{queue}.retry.{failedAttempt}";

    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        MessagingOptions messaging,
        IntegrationEventRegistry registry,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        foreach (var consumer in registry.Consumers.GroupBy(c => c.Queue))
        {
            await channel.QueueDeclareAsync(
                consumer.Key, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

            foreach (var registration in consumer)
            {
                await channel.QueueBindAsync(
                    consumer.Key,
                    options.Exchange,
                    registry.ContractOf(registration.EventType).RoutingKey,
                    cancellationToken: cancellationToken);
            }

            for (var attempt = 1; attempt < messaging.MaxAttempts; attempt++)
            {
                var arguments = new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = (long)messaging.RetryDelays[attempt - 1].TotalMilliseconds,
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = consumer.Key,
                };

                await channel.QueueDeclareAsync(
                    RetryQueue(consumer.Key, attempt),
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: arguments,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
