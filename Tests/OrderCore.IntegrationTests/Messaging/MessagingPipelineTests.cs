using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using RabbitMQ.Client;
using Xunit;

namespace OrderCore.IntegrationTests.Messaging;

/// <summary>
/// The messaging pipeline end to end, through a real broker and database,
/// with a test module of its own (an outbox, an inbox and a handler that
/// records what it saw): relay → exchange → consumer queue → inbox →
/// handler, and the failure paths — duplicates, a handler that never
/// succeeds, a publish the broker refuses.
/// </summary>
public sealed class MessagingPipelineTests : IClassFixture<MessagingPipelineTests.Host>
{
    private const string Queue = "tests.recorder";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly Host _host;

    public MessagingPipelineTests(Host host)
    {
        _host = host;
    }

    [Fact]
    public async Task An_event_written_to_the_outbox_reaches_its_handler_in_the_same_trace()
    {
        var happened = SomethingHappened.New("hello");
        using var request = new Activity("test-request").Start();

        await _host.EnqueueAsync(happened);
        request.Stop();

        var handled = await EventuallyAsync(() => _host.HandledAsync(happened.EventId), records => records.Count == 1);
        handled.Single().Note.Should().Be("hello");
        handled.Single().TraceId.Should().Be(request.TraceId.ToString());
        handled.Single().CausationId.Should().Be(happened.EventId, "whatever the handler publishes is caused by this message");

        var row = await _host.OutboxRowAsync(happened.EventId);
        row.SentAt.Should().NotBeNull();
        row.TraceParent.Should().Be(request.Id);
    }

    [Fact]
    public async Task A_message_delivered_twice_is_handled_once()
    {
        var happened = SomethingHappened.New("twice");
        var envelope = new MessageEnvelope(
            happened.EventId,
            SomethingHappened.Contract,
            1,
            happened.OccurredAt,
            null,
            System.Text.Json.JsonSerializer.SerializeToElement(happened, MessageEnvelope.JsonOptions));

        await _host.PublishRawAsync(envelope);
        await _host.PublishRawAsync(envelope);

        await EventuallyAsync(() => _host.HandledAsync(happened.EventId), records => records.Count == 1);
        await Task.Delay(TimeSpan.FromSeconds(1));

        (await _host.HandledAsync(happened.EventId)).Should().HaveCount(1);
        _host.Probe.AttemptsOf(happened.EventId).Should().Be(1, "the second delivery is recognised by the inbox before the handler runs");
    }

    [Fact]
    public async Task A_handler_that_keeps_failing_ends_in_failed_messages_after_five_attempts_without_blocking_the_queue()
    {
        var failing = SomethingHappened.New("never works", fail: true);
        var next = SomethingHappened.New("after the failing one");
        using var request = new Activity("test-request").Start();

        await _host.EnqueueAsync(failing);
        await _host.EnqueueAsync(next);
        request.Stop();

        await EventuallyAsync(() => _host.HandledAsync(next.EventId), records => records.Count == 1);
        var failed = await EventuallyAsync(() => _host.FailedAsync(failing.EventId), rows => rows.Count == 1);

        failed.Single().Attempts.Should().Be(5);
        failed.Single().Consumer.Should().Be(Queue);
        failed.Single().Type.Should().Be(SomethingHappened.Contract);
        failed.Single().Status.Should().Be("Pending");
        failed.Single().LastError.Should().Contain("handler refused");
        failed.Single().TraceParent.Should().Be(request.Id, "the backoffice can find the trace that led to the failure");
        _host.Probe.AttemptsOf(failing.EventId).Should().Be(5);
        (await _host.HandledAsync(failing.EventId)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_publish_the_broker_refuses_leaves_the_row_unsent_until_publishing_works_again()
    {
        await _host.DeleteExchangeAsync();
        try
        {
            var happened = SomethingHappened.New("while the exchange is gone");
            await _host.EnqueueAsync(happened);

            var row = await EventuallyAsync(() => _host.OutboxRowAsync(happened.EventId), r => r.PublishAttempts > 0);
            row.SentAt.Should().BeNull();
            row.LastError.Should().NotBeNullOrEmpty();

            await _host.RestoreExchangeAsync();

            // The row is published once the broker accepts it again. Whether
            // this one reaches the handler depends on timing: restoring is two
            // steps (exchange, then binding), and a publish landing between
            // them is accepted and dropped as unroutable — the broker's normal
            // answer for an event nobody subscribes to. What follows the
            // restore does reach it.
            await EventuallyAsync(() => _host.OutboxRowAsync(happened.EventId), r => r.SentAt is not null);
            var next = SomethingHappened.New("after the exchange is back");
            await _host.EnqueueAsync(next);
            await EventuallyAsync(() => _host.HandledAsync(next.EventId), records => records.Count == 1);
        }
        finally
        {
            await _host.RestoreExchangeAsync();
        }
    }

    [Fact]
    public async Task A_failed_message_is_listed_and_once_its_cause_is_gone_replaying_it_lets_it_through()
    {
        var failing = SomethingHappened.New("fails until fixed", fail: true);
        await _host.EnqueueAsync(failing);
        var failed = (await EventuallyAsync(() => _host.FailedAsync(failing.EventId), rows => rows.Count == 1)).Single();
        var admin = _host.CreateAdminClient();

        var pending = await admin.GetFromJsonAsync<JsonElement>("/api/messaging/failed-messages?status=Pending&pageSize=100", Json);
        var listed = pending.GetProperty("items").EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == failed.Id);
        listed.GetProperty("attempts").GetInt32().Should().Be(5);
        listed.GetProperty("consumer").GetString().Should().Be(Queue);
        listed.GetProperty("type").GetString().Should().Be(SomethingHappened.Contract);
        var details = await admin.GetFromJsonAsync<JsonElement>($"/api/messaging/failed-messages/{failed.Id}", Json);
        details.GetProperty("body").GetString().Should().Contain(failing.EventId.ToString());

        _host.Probe.Fix(failing.EventId);
        var replay = await admin.PostAsync($"/api/messaging/failed-messages/{failed.Id}/replay", null);

        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("Replayed");
        await EventuallyAsync(() => _host.HandledAsync(failing.EventId), records => records.Count == 1);
        _host.Probe.AttemptsOf(failing.EventId).Should().Be(6, "five failed attempts, then the replay");

        var again = await admin.PostAsync($"/api/messaging/failed-messages/{failed.Id}/replay", null);
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await again.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("invalid_failed_message_state");
    }

    [Fact]
    public async Task A_discarded_message_is_never_sent_again()
    {
        var failing = SomethingHappened.New("given up on", fail: true);
        await _host.EnqueueAsync(failing);
        var failed = (await EventuallyAsync(() => _host.FailedAsync(failing.EventId), rows => rows.Count == 1)).Single();
        var admin = _host.CreateAdminClient();

        var discard = await admin.PostAsync($"/api/messaging/failed-messages/{failed.Id}/discard", null);
        discard.StatusCode.Should().Be(HttpStatusCode.OK);
        (await discard.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("Discarded");

        var replay = await admin.PostAsync($"/api/messaging/failed-messages/{failed.Id}/replay", null);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        _host.Probe.AttemptsOf(failing.EventId).Should().Be(5);

        var discarded = await admin.GetFromJsonAsync<JsonElement>("/api/messaging/failed-messages?status=Discarded&pageSize=100", Json);
        discarded.GetProperty("items").EnumerateArray().Should().Contain(m => m.GetProperty("id").GetGuid() == failed.Id);
    }

    [Fact]
    public async Task An_unknown_failed_message_is_not_found()
    {
        var response = await _host.CreateAdminClient().PostAsync($"/api/messaging/failed-messages/{Guid.NewGuid()}/replay", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("failed_message_not_found");
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var value = await read();
            if (done(value))
            {
                return value;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Condition not met within {Timeout}; last value: {value}.");
            }

            await Task.Delay(100);
        }
    }

    /// <summary>The database, the host with the test module plugged in, and the test's own broker connection.</summary>
    public sealed class Host : IAsyncLifetime
    {
        private readonly ApiDatabase _database = new();
        private WebApplicationFactory<Program> _factory = null!;
        private RabbitMqOptions _broker = null!;
        private IConnection _connection = null!;

        public HandlerProbe Probe => _factory.Services.GetRequiredService<HandlerProbe>();

        public HttpClient CreateAdminClient() => _factory.CreateAdminClient();

        public async Task InitializeAsync()
        {
            await _database.InitializeAsync();

            await using (var db = new TestModuleDbContext(_database.Options<TestModuleDbContext>()))
            {
                await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            }

            _factory = _database.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddDbContext<TestModuleDbContext>(options => options.UseNpgsql(_database.ConnectionString));
                services.AddScoped<OutboxWriter<TestModuleDbContext>>();
                services.AddSingleton<HandlerProbe>();
                services.AddIntegrationEvent<SomethingHappened>(SomethingHappened.Contract, 1);
                services.AddOutboxSource<TestModuleDbContext>();
                services.AddIntegrationEventConsumer<SomethingHappened, RecordingHandler, TestModuleDbContext>(Queue);
            }));

            _broker = _factory.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            _connection = await TestBroker.ConnectAsync(_broker.VirtualHost);
        }

        public async Task DisposeAsync()
        {
            await _connection.DisposeAsync();
            await _factory.DisposeAsync();
            await _database.DisposeAsync();
        }

        public async Task EnqueueAsync(IntegrationEvent integrationEvent)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<OutboxWriter<TestModuleDbContext>>().Enqueue(integrationEvent);
            await scope.ServiceProvider.GetRequiredService<TestModuleDbContext>().SaveChangesAsync();
        }

        public async Task PublishRawAsync(MessageEnvelope envelope)
        {
            await using var channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
            await channel.BasicPublishAsync(
                _broker.Exchange,
                IntegrationEventRegistry.RoutingKey(envelope.Type, envelope.Version),
                mandatory: true,
                new BasicProperties { MessageId = envelope.MessageId.ToString(), DeliveryMode = DeliveryModes.Persistent },
                envelope.ToBytes());
        }

        public async Task DeleteExchangeAsync()
        {
            await using var channel = await _connection.CreateChannelAsync();
            await channel.ExchangeDeleteAsync(_broker.Exchange);
        }

        /// <summary>What the API declares at startup: the exchange and the queue's binding (deleting the exchange removed it).</summary>
        public async Task RestoreExchangeAsync()
        {
            await using var channel = await _connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync(_broker.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            await channel.QueueBindAsync(Queue, _broker.Exchange, IntegrationEventRegistry.RoutingKey(SomethingHappened.Contract, 1));
        }

        public async Task<List<HandledRecord>> HandledAsync(Guid messageId)
        {
            await using var db = new TestModuleDbContext(_database.Options<TestModuleDbContext>());
            return await db.Handled.Where(h => h.MessageId == messageId).ToListAsync();
        }

        public async Task<OutboxMessage> OutboxRowAsync(Guid messageId)
        {
            await using var db = new TestModuleDbContext(_database.Options<TestModuleDbContext>());
            return await db.Set<OutboxMessage>().SingleAsync(m => m.Id == messageId);
        }

        public async Task<List<Api.Modules.Messaging.Infrastructure.Persistence.Models.FailedMessagePersistenceModel>> FailedAsync(Guid messageId)
        {
            await using var db = new MessagingDbContext(_database.Options<MessagingDbContext>());
            return await db.FailedMessages.Where(f => f.MessageId == messageId).ToListAsync();
        }
    }

    public sealed record SomethingHappened : IntegrationEvent
    {
        public const string Contract = "tests.something-happened";

        public required string Note { get; init; }

        public bool Fail { get; init; }

        public static SomethingHappened New(string note, bool fail = false) => new()
        {
            EventId = Guid.NewGuid(),
            Version = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            Note = note,
            Fail = fail,
        };
    }

    /// <summary>Counts every time the handler runs, including the runs that throw (and so leave nothing in the database).</summary>
    public sealed class HandlerProbe
    {
        private readonly ConcurrentDictionary<Guid, int> _attempts = new();

        public void Record(Guid messageId) => _attempts.AddOrUpdate(messageId, 1, (_, count) => count + 1);

        public int AttemptsOf(Guid messageId) => _attempts.GetValueOrDefault(messageId);

        private readonly ConcurrentDictionary<Guid, bool> _fixed = new();

        /// <summary>The cause of the failure is gone: the handler accepts this message from now on.</summary>
        public void Fix(Guid messageId) => _fixed[messageId] = true;

        public bool IsFixed(Guid messageId) => _fixed.ContainsKey(messageId);
    }

    public sealed class RecordingHandler : IIntegrationEventHandler<SomethingHappened>
    {
        private readonly TestModuleDbContext _db;
        private readonly HandlerProbe _probe;
        private readonly IMessageContext _messageContext;

        public RecordingHandler(TestModuleDbContext db, HandlerProbe probe, IMessageContext messageContext)
        {
            _db = db;
            _probe = probe;
            _messageContext = messageContext;
        }

        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken cancellationToken)
        {
            _probe.Record(integrationEvent.EventId);
            if (integrationEvent.Fail && !_probe.IsFixed(integrationEvent.EventId))
            {
                throw new InvalidOperationException("The test handler refused this message.");
            }

            _db.Handled.Add(new HandledRecord
            {
                Id = Guid.NewGuid(),
                MessageId = integrationEvent.EventId,
                Note = integrationEvent.Note,
                TraceId = Activity.Current?.TraceId.ToString(),
                CausationId = _messageContext.CausationId,
            });
            return Task.CompletedTask;
        }
    }

    public sealed class HandledRecord
    {
        public Guid Id { get; set; }

        public Guid MessageId { get; set; }

        public string Note { get; set; } = string.Empty;

        public string? TraceId { get; set; }

        public Guid? CausationId { get; set; }
    }

    public sealed class TestModuleDbContext : DbContext
    {
        public TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : base(options)
        {
        }

        public DbSet<HandledRecord> Handled => Set<HandledRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AddOutbox("tests").AddInbox("tests");
            modelBuilder.Entity<HandledRecord>(builder =>
            {
                builder.ToTable("tests_handled");
                builder.Property(h => h.Id).ValueGeneratedNever();
            });
        }
    }
}
