using System.Text.Json;
using FluentAssertions;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using Xunit;

namespace OrderCore.UnitTests.Messaging;

public sealed class MessagingContractTests
{
    private sealed record ThingShipped : IntegrationEvent
    {
        public required Guid OrderId { get; init; }

        public required decimal Amount { get; init; }
    }

    private static readonly ThingShipped Shipped = new()
    {
        EventId = Guid.NewGuid(),
        Version = 2,
        OccurredAt = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
        OrderId = Guid.NewGuid(),
        Amount = 199.90m,
    };

    [Fact]
    public void An_outbox_row_travels_as_an_envelope_and_comes_back_as_the_same_event()
    {
        var causation = Guid.NewGuid();
        var row = new OutboxMessage
        {
            Id = Shipped.EventId,
            Type = "tests.thing-shipped",
            Version = 2,
            PayloadJson = MessageEnvelope.SerializePayload(Shipped),
            OccurredAt = Shipped.OccurredAt,
            CausationId = causation,
        };

        var received = MessageEnvelope.FromBytes(MessageEnvelope.FromOutbox(row).ToBytes());

        received.MessageId.Should().Be(Shipped.EventId);
        received.Type.Should().Be("tests.thing-shipped");
        received.Version.Should().Be(2);
        received.OccurredAt.Should().Be(Shipped.OccurredAt);
        received.CausationId.Should().Be(causation);
        received.ToEvent(typeof(ThingShipped)).Should().Be(Shipped);
    }

    [Fact]
    public void A_body_that_is_not_an_envelope_is_rejected_as_unreadable()
    {
        var read = () => MessageEnvelope.FromBytes("null"u8);

        read.Should().Throw<JsonException>();
    }

    [Fact]
    public void Routing_keys_carry_the_contract_version()
    {
        IntegrationEventRegistry.RoutingKey("payments.payment-authorized", 1).Should().Be("payments.payment-authorized.v1");
    }

    [Fact]
    public void Five_attempts_mean_four_waiting_queues_one_per_failed_attempt()
    {
        var options = new MessagingOptions();

        options.MaxAttempts.Should().Be(5);
        options.RetryDelays.Should().Equal(
            TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));
        Enumerable.Range(1, options.MaxAttempts - 1)
            .Select(attempt => RabbitMqTopology.RetryQueue("orders.payment-results", attempt))
            .Should().Equal(
                "orders.payment-results.retry.1",
                "orders.payment-results.retry.2",
                "orders.payment-results.retry.3",
                "orders.payment-results.retry.4");
    }
}
