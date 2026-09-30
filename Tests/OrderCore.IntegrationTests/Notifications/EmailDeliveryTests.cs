using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Application.UseCases;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Models;
using Xunit;

namespace OrderCore.IntegrationTests.Notifications;

/// <summary>
/// E-mail through the real host, database and SMTP (Mailpit): a queued
/// message arrives, rendered in Portuguese; one that can't be sent is retried
/// and then given up on, its content erased either way (password-recovery
/// spec, decision 7); old records are removed.
/// </summary>
public sealed class EmailDeliveryTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public EmailDeliveryTests(ApiDatabase database)
    {
        _database = database;
    }

    private static Dictionary<string, string> ResetValues(string link) => new()
    {
        ["name"] = "Jane",
        ["link"] = link,
        ["validFor"] = "30 minutos",
    };

    private static async Task<Guid> QueueAsync(WebApplicationFactory<Program> factory, string to, string link)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<QueueEmailUseCase>()
            .ExecuteAsync(to, EmailTemplateNames.PasswordReset, ResetValues(link), CancellationToken.None);
    }

    private async Task<EmailMessagePersistenceModel> WaitForRecordAsync(Guid id, string status)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (true)
        {
            await using var db = new NotificationsDbContext(_database.Options<NotificationsDbContext>());
            var record = await db.Emails.AsNoTracking().SingleAsync(m => m.Id == id);
            if (record.Status == status)
            {
                return record;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"E-mail {id} is still {record.Status}, expected {status}.");
            }

            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task A_queued_email_arrives_and_its_record_keeps_no_content()
    {
        await using var factory = _database.CreateFactory();
        var to = TestMailpit.NewAddress("jane");

        var id = await QueueAsync(factory, to, "https://shop.example/redefinir-senha?token=abc&x=1");

        var email = await TestMailpit.WaitForMessageAsync(to);
        email.Subject.Should().Be("Redefinição de senha");
        email.From.Should().Be("no-reply@ordercore.local");
        email.Html.Should().Contain("Olá, Jane.").And.Contain("href=\"https://shop.example/redefinir-senha?token=abc&amp;x=1\"");
        email.Text.Should().Contain("https://shop.example/redefinir-senha?token=abc&x=1");
        email.MessageId.Should().StartWith(id.ToString("N"));

        var record = await WaitForRecordAsync(id, "Sent");
        record.Attempts.Should().Be(1);
        record.HtmlBody.Should().BeEmpty();
        record.TextBody.Should().BeEmpty();
        record.To.Should().Be($"j***@example.com");
        (await TestMailpit.CountAsync(to)).Should().Be(1, "sent once");
    }

    [Fact]
    public async Task An_email_that_cant_be_sent_is_retried_then_given_up_with_its_content_erased()
    {
        await using var factory = _database.CreateFactory().WithWebHostBuilder(builder =>
        {
            // Nothing listens there: every attempt fails to connect.
            builder.UseSetting("Notifications:Smtp:Host", "127.0.0.1");
            builder.UseSetting("Notifications:Smtp:Port", "1");
        });
        var to = TestMailpit.NewAddress("unlucky");

        var id = await QueueAsync(factory, to, "https://shop.example/redefinir-senha?token=secret");

        var record = await WaitForRecordAsync(id, "Failed");
        record.Attempts.Should().Be(5);
        record.FailedAt.Should().NotBeNull();
        record.HtmlBody.Should().BeEmpty();
        record.TextBody.Should().BeEmpty();
        record.To.Should().Be("u***@example.com");
        record.LastError.Should().NotBeNullOrEmpty().And.NotContain(to);
        (await TestMailpit.CountAsync(to)).Should().Be(0);
    }

    [Fact]
    public async Task Records_past_their_retention_are_removed_and_pending_ones_kept()
    {
        var old = OldRecord("Sent", sentAt: DateTimeOffset.UtcNow.AddDays(-91));
        var recent = OldRecord("Failed", failedAt: DateTimeOffset.UtcNow.AddDays(-10));
        await using (var db = new NotificationsDbContext(_database.Options<NotificationsDbContext>()))
        {
            db.Emails.AddRange(old, recent);
            await db.SaveChangesAsync();
        }

        // Retention runs on the dispatcher's first pass.
        await using var factory = _database.CreateFactory();
        factory.CreateClient().Dispose();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (true)
        {
            await using var db = new NotificationsDbContext(_database.Options<NotificationsDbContext>());
            if (!await db.Emails.AnyAsync(m => m.Id == old.Id))
            {
                (await db.Emails.AnyAsync(m => m.Id == recent.Id)).Should().BeTrue();
                return;
            }

            DateTimeOffset.UtcNow.Should().BeBefore(deadline, "the old record should have been removed");
            await Task.Delay(100);
        }
    }

    private static EmailMessagePersistenceModel OldRecord(string status, DateTimeOffset? sentAt = null, DateTimeOffset? failedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        To = "j***@example.com",
        Template = EmailTemplateNames.PasswordReset,
        Subject = "Redefinição de senha",
        Status = status,
        Attempts = 1,
        NextAttemptAt = (sentAt ?? failedAt)!.Value,
        CreatedAt = (sentAt ?? failedAt)!.Value,
        SentAt = sentAt,
        FailedAt = failedAt,
        Version = 2,
    };
}
