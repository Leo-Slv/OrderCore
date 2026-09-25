using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Infrastructure.Outbox;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// Same shape as EfCustomerRepositoryTests/EfOrderRepositoryTests: exercises
/// the real PostgreSQL provider and the InitialPaymentsSchema migration,
/// plus a full outbox round trip (enqueue → same transaction as the
/// Payment write → a manual "publish" pass reads it back), since that is
/// the actual point of this feature.
/// </summary>
public sealed class EfPaymentRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PaymentsDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var dbContext = new PaymentsDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private PaymentsDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PaymentsDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task AddAsync_then_GetByOrderIdAsync_round_trips_a_payment_with_a_refund()
    {
        var orderId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var payment = Payment.Create(orderId, 100m, "BRL", PaymentMethod.Pix, "idem-1", "Fake", null, DateTimeOffset.UtcNow);
            payment.MarkProcessing();
            payment.Authorize("provider-ref", DateTimeOffset.UtcNow);
            payment.Capture(DateTimeOffset.UtcNow);
            payment.RequestRefund(30m, "customer request", DateTimeOffset.UtcNow);

            await repository.AddAsync(payment, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var reloaded = await repository.GetByOrderIdAsync(orderId, CancellationToken.None);

            reloaded.Should().NotBeNull();
            reloaded!.Status.Should().Be(PaymentStatus.Captured);
            reloaded.Method.Should().Be(PaymentMethod.Pix);
            reloaded.Refunds.Should().ContainSingle(r => r.Amount == 30m);
        }
    }

    [Fact]
    public async Task Enqueued_outbox_message_commits_with_the_payment_write_and_can_be_published()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.Empty;

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var outbox = new OutboxWriter(dbContext);
            var payment = Payment.Create(orderId, 50m, "BRL", PaymentMethod.Card, "idem-2", "Fake", null, DateTimeOffset.UtcNow);
            payment.MarkProcessing();
            payment.Authorize("provider-ref", DateTimeOffset.UtcNow);
            paymentId = payment.Id;

            await repository.AddAsync(payment, CancellationToken.None);
            outbox.Enqueue(new PaymentAuthorized
            {
                EventId = Guid.NewGuid(),
                Version = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                OrderId = orderId,
                PaymentId = paymentId,
                Amount = 50m,
                Currency = "BRL",
            });

            // Single SaveChangesAsync flushes both the Payment insert and
            // the OutboxMessage in the same transaction — the actual point
            // of "transactional outbox".
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var pending = await dbContext.OutboxMessages.Where(m => m.ProcessedAt == null).ToListAsync();

            pending.Should().ContainSingle(m => m.Type == nameof(PaymentAuthorized));
        }
    }

    [Fact]
    public async Task Confirming_a_capture_persists_CapturedAt()
    {
        var orderId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var payment = Payment.Create(orderId, 20m, "BRL", PaymentMethod.Card, "idem-3", "Fake", null, DateTimeOffset.UtcNow);
            payment.MarkProcessing();
            payment.Authorize("provider-ref", DateTimeOffset.UtcNow);
            await repository.AddAsync(payment, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var payment = await repository.GetByOrderIdAsync(orderId, CancellationToken.None);
            payment!.Capture(DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var reloaded = await repository.GetByOrderIdAsync(orderId, CancellationToken.None);

            reloaded!.CapturedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Payment_method_migration_treats_existing_payments_as_card()
    {
        var orderId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync("20260924120041_InitialPaymentsSchema");

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO payments ("Id", "OrderId", "Amount", "Currency", "Status", "IdempotencyKey", "Provider", "CreatedAt", "UpdatedAt", "Version")
                VALUES ({0}, {1}, 100, 'BRL', 'Authorized', 'idem-legacy', 'Fake', now(), now(), 1);
                """,
                Guid.NewGuid(), orderId);

            await migrator.MigrateAsync();
        }

        await using (var dbContext = CreateDbContext())
        {
            var reloaded = await new EfPaymentRepository(dbContext).GetByOrderIdAsync(orderId, CancellationToken.None);

            reloaded!.Method.Should().Be(PaymentMethod.Card);
        }
    }

    [Fact]
    public async Task Requesting_a_refund_on_an_already_saved_payment_inserts_it()
    {
        var orderId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var payment = Payment.Create(orderId, 100m, "BRL", PaymentMethod.Card, "idem-refund-later", "Fake", null, DateTimeOffset.UtcNow);
            payment.MarkProcessing();
            payment.Authorize("provider-ref", DateTimeOffset.UtcNow);
            payment.Capture(DateTimeOffset.UtcNow);
            await repository.AddAsync(payment, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var payment = await repository.GetByOrderIdAsync(orderId, CancellationToken.None);
            payment!.RequestRefund(40m, "customer request", DateTimeOffset.UtcNow);

            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var reloaded = await new EfPaymentRepository(dbContext).GetByOrderIdAsync(orderId, CancellationToken.None);

            reloaded!.Refunds.Should().ContainSingle(r => r.Amount == 40m);
        }
    }

    [Fact]
    public async Task Voiding_an_already_saved_payment_persists_the_status_and_VoidedAt()
    {
        var orderId = Guid.NewGuid();
        var voidedAt = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var payment = Payment.Create(orderId, 100m, "BRL", PaymentMethod.Card, "idem-void", "Fake", null, DateTimeOffset.UtcNow);
            payment.MarkProcessing();
            payment.Authorize("provider-ref", DateTimeOffset.UtcNow);
            await repository.AddAsync(payment, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            var payment = await repository.GetByOrderIdAsync(orderId, CancellationToken.None);
            payment!.Void(voidedAt);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var reloaded = await new EfPaymentRepository(dbContext).GetByOrderIdAsync(orderId, CancellationToken.None);

            reloaded!.Status.Should().Be(PaymentStatus.Voided);
            reloaded.VoidedAt.Should().Be(voidedAt);
        }
    }

    [Fact]
    public async Task ListAsync_filters_by_status_method_and_created_range_newest_first()
    {
        var start = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);
            for (var day = 0; day < 4; day++)
            {
                var method = day % 2 == 0 ? PaymentMethod.Card : PaymentMethod.Pix;
                var payment = Payment.Create(Guid.NewGuid(), 10m + day, "BRL", method, $"idem-list-{day}", "Fake", null, start.AddDays(day));
                payment.MarkProcessing();
                payment.Authorize("ref", start.AddDays(day));
                await repository.AddAsync(payment, CancellationToken.None);
            }

            var failed = Payment.Create(Guid.NewGuid(), 99m, "BRL", PaymentMethod.Card, "idem-list-failed", "Fake", null, start.AddDays(1));
            failed.Fail("card_declined");
            await repository.AddAsync(failed, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfPaymentRepository(dbContext);

            var (authorizedCards, cardTotal) = await repository.ListAsync(
                new ListPaymentsFilter { Status = PaymentStatus.Authorized, Method = PaymentMethod.Card }, CancellationToken.None);
            cardTotal.Should().Be(2);
            authorizedCards.Select(p => p.Amount).Should().Equal(12m, 10m);

            var (inRange, rangeTotal) = await repository.ListAsync(
                new ListPaymentsFilter { CreatedFrom = start.AddDays(1), CreatedTo = start.AddDays(3) }, CancellationToken.None);
            rangeTotal.Should().Be(3);
            inRange.Select(p => p.CreatedAt).Should().BeInDescendingOrder();

            var (secondPage, allTotal) = await repository.ListAsync(
                new ListPaymentsFilter { Page = 2, PageSize = 2 }, CancellationToken.None);
            allTotal.Should().Be(5);
            secondPage.Should().HaveCount(2);
        }
    }
}
