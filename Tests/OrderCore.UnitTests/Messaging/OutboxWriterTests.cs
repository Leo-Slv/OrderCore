using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using Xunit;

namespace OrderCore.UnitTests.Messaging;

/// <summary>Events enqueued together keep their order, even with the same timestamp.</summary>
public sealed class OutboxWriterTests
{
    private sealed record SomethingHappened : IntegrationEvent
    {
        public required string Step { get; init; }
    }

    private sealed class OutboxOnly(DbContextOptions<OutboxOnly> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutbox("tests");
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static (OutboxWriter<OutboxOnly> Writer, OutboxOnly Db) Writer()
    {
        // Nothing is saved: adding to the context never reaches the database.
        var db = new OutboxOnly(new DbContextOptionsBuilder<OutboxOnly>().UseNpgsql("Host=unused").Options);
        var registry = new ServiceCollection().AddIntegrationEvent<SomethingHappened>("tests.something-happened", 1).IntegrationEventRegistry();
        return (new OutboxWriter<OutboxOnly>(db, registry, new MessageContext()), db);
    }

    private static SomethingHappened Event(string step, DateTimeOffset at) =>
        new() { EventId = Guid.NewGuid(), Version = 1, OccurredAt = at, Step = step };

    private static List<OutboxMessage> Rows(OutboxOnly db) =>
        db.ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity).ToList();

    [Fact]
    public void Events_with_the_same_timestamp_are_spaced_in_the_order_they_were_enqueued()
    {
        var (writer, db) = Writer();

        writer.Enqueue(Event("created", Now));
        writer.Enqueue(Event("payment requested", Now));
        writer.Enqueue(Event("reserved", Now));

        var rows = Rows(db).OrderBy(r => r.OccurredAt).ToList();
        rows.Select(r => MessageEnvelope.FromOutbox(r).Payload.GetProperty("step").GetString())
            .Should().Equal("created", "payment requested", "reserved");
        rows.Select(r => r.OccurredAt).Should().Equal(Now, Now.AddMicroseconds(1), Now.AddMicroseconds(2));
        rows.Should().OnlyContain(r => MessageEnvelope.FromOutbox(r).Payload.GetProperty("occurredAt").GetDateTimeOffset() == r.OccurredAt,
            "the payload says the same as the row");
    }

    [Fact]
    public void Later_events_keep_their_own_timestamp()
    {
        var (writer, db) = Writer();

        writer.Enqueue(Event("first", Now));
        writer.Enqueue(Event("second", Now.AddSeconds(3)));

        Rows(db).Select(r => r.OccurredAt).Should().BeEquivalentTo([Now, Now.AddSeconds(3)]);
    }
}
