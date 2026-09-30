using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Models;
using OrderCore.Api.Modules.Messaging.Infrastructure.Retention;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using Xunit;

namespace OrderCore.IntegrationTests.Messaging;

/// <summary>
/// Retention of technical records (production-readiness spec, decision 6)
/// against a real database: sent outbox rows and handled inbox rows past 30
/// days go — in batches, here of 2 — while recent ones, unsent ones and
/// pending failed messages stay; resolved failed messages go after 90 days.
/// Payments' inbox (the Stripe webhooks) is cleaned like the consumers'.
/// </summary>
public sealed class RetentionTests : IClassFixture<ApiDatabase>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly ApiDatabase _database;

    public RetentionTests(ApiDatabase database)
    {
        _database = database;
    }

    private static OutboxMessage Outbox(DateTimeOffset occurredAt, DateTimeOffset? sentAt) => new()
    {
        Id = Guid.NewGuid(),
        Type = "retention.test",
        Version = 1,
        PayloadJson = "{}",
        OccurredAt = occurredAt,
        SentAt = sentAt,
    };

    private static InboxMessage Inbox(DateTimeOffset processedAt) =>
        new() { MessageId = Guid.NewGuid(), Consumer = "retention.test", ProcessedAt = processedAt };

    private static FailedMessagePersistenceModel Failed(string status, DateTimeOffset failedAt, DateTimeOffset? resolvedAt) => new()
    {
        Id = Guid.NewGuid(),
        MessageId = Guid.NewGuid(),
        Type = "retention.test",
        ContractVersion = 1,
        Consumer = "retention.test",
        Body = "{}",
        LastError = "test",
        Attempts = 5,
        FirstFailedAt = failedAt,
        LastFailedAt = failedAt,
        Status = status,
        ResolvedAt = resolvedAt,
        Version = 1,
    };

    [Fact]
    public async Task Old_technical_records_are_removed_and_everything_else_is_kept()
    {
        var old = Now.AddDays(-40);
        var recent = Now.AddDays(-5);

        var oldSent = Enumerable.Range(0, 5).Select(_ => Outbox(old, old)).ToList();
        var recentSent = Outbox(recent, recent);
        var oldUnsent = Outbox(old, sentAt: null);
        var oldHandled = Inbox(old);
        var recentHandled = Inbox(recent);
        var oldWebhook = Inbox(old);
        var resolvedLongAgo = Failed("Replayed", Now.AddDays(-120), resolvedAt: Now.AddDays(-100));
        var resolvedRecently = Failed("Discarded", Now.AddDays(-60), resolvedAt: Now.AddDays(-60));
        var stillPending = Failed("Pending", Now.AddDays(-200), resolvedAt: null);

        await using (var orders = new OrdersDbContext(_database.Options<OrdersDbContext>()))
        {
            orders.Set<OutboxMessage>().AddRange([.. oldSent, recentSent, oldUnsent]);
            orders.Set<InboxMessage>().AddRange(oldHandled, recentHandled);
            await orders.SaveChangesAsync();
        }

        await using (var payments = new PaymentsDbContext(_database.Options<PaymentsDbContext>()))
        {
            payments.Set<InboxMessage>().Add(oldWebhook);
            await payments.SaveChangesAsync();
        }

        await using (var messaging = new MessagingDbContext(_database.Options<MessagingDbContext>()))
        {
            messaging.FailedMessages.AddRange(resolvedLongAgo, resolvedRecently, stillPending);
            await messaging.SaveChangesAsync();
        }

        await using var factory = _database.CreateFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("Messaging:Retention:BatchSize", "2"));
        var removed = await factory.Services.GetRequiredService<RetentionCleaner>().RunAsync(Now, CancellationToken.None);

        removed.Should().BeGreaterThanOrEqualTo(8, "5 sent outbox rows, 2 inbox rows and 1 resolved failed message were past their time");

        await using (var orders = new OrdersDbContext(_database.Options<OrdersDbContext>()))
        {
            var outboxIds = await orders.Set<OutboxMessage>().Select(m => m.Id).ToListAsync();
            outboxIds.Should().NotContain(oldSent.Select(m => m.Id)).And.Contain([recentSent.Id, oldUnsent.Id]);
            var inboxIds = await orders.Set<InboxMessage>().Select(m => m.MessageId).ToListAsync();
            inboxIds.Should().NotContain(oldHandled.MessageId).And.Contain(recentHandled.MessageId);
        }

        await using (var payments = new PaymentsDbContext(_database.Options<PaymentsDbContext>()))
        {
            (await payments.Set<InboxMessage>().AnyAsync(m => m.MessageId == oldWebhook.MessageId)).Should().BeFalse();
        }

        await using (var messaging = new MessagingDbContext(_database.Options<MessagingDbContext>()))
        {
            var failedIds = await messaging.FailedMessages.Select(m => m.Id).ToListAsync();
            failedIds.Should().NotContain(resolvedLongAgo.Id).And.Contain([resolvedRecently.Id, stillPending.Id]);
        }
    }
}
