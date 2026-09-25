using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using Xunit;

namespace OrderCore.UnitTests.AuditLogs;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task RecordAsync_persists_an_audit_log_with_the_given_fields()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser(), NullLogger<AuditLogService>.Instance);
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        await service.RecordAsync(
            "OrderCreated", "Order", entityId, new Dictionary<string, string?> { ["customerId"] = "abc" }, userId, CancellationToken.None);

        var stored = repository.Stored;
        stored.Should().ContainSingle();
        stored.Single().Action.Should().Be("OrderCreated");
        stored.Single().UserId.Should().Be(userId);
        stored.Single().MetadataJson.Should().Contain("customerId");
    }

    [Fact]
    public async Task RecordAsync_accepts_null_metadata_and_null_userId()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser(), NullLogger<AuditLogService>.Instance);
        var entityId = Guid.NewGuid();

        await service.RecordAsync("InventoryReserved", "InventoryReservation", entityId, null, null, CancellationToken.None);

        var stored = repository.Stored;
        stored.Single().UserId.Should().BeNull();
        stored.Single().MetadataJson.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_drops_blank_metadata_entries()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser(), NullLogger<AuditLogService>.Instance);
        var entityId = Guid.NewGuid();

        await service.RecordAsync(
            "ProductCreated",
            "Product",
            entityId,
            new Dictionary<string, string?> { ["sku"] = "SKU-1", ["blank"] = "  ", ["nullValue"] = null },
            userId: null,
            CancellationToken.None);

        var stored = repository.Stored;
        stored.Single().MetadataJson.Should().Contain("sku").And.NotContain("blank").And.NotContain("nullValue");
    }

    [Fact]
    public async Task RecordAsync_without_a_userId_records_the_signed_in_user()
    {
        var repository = new FakeAuditLogRepository();
        var signedIn = new FakeCurrentUser { UserId = Guid.NewGuid(), Role = "Admin" };
        var service = new AuditLogService(repository, TimeProvider.System, signedIn, NullLogger<AuditLogService>.Instance);
        var entityId = Guid.NewGuid();

        await service.RecordAsync("ProductPublished", "Product", entityId, null, null, CancellationToken.None);

        var stored = repository.Stored;
        stored.Single().UserId.Should().Be(signedIn.UserId);
    }

    [Fact]
    public async Task RecordAsync_with_an_explicit_userId_keeps_it_over_the_signed_in_user()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser { UserId = Guid.NewGuid() }, NullLogger<AuditLogService>.Instance);
        var explicitUserId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        await service.RecordAsync("OrderCreated", "Order", entityId, null, explicitUserId, CancellationToken.None);

        var stored = repository.Stored;
        stored.Single().UserId.Should().Be(explicitUserId);
    }

    [Fact]
    public async Task RecordAsync_does_not_throw_when_the_entry_cannot_be_saved()
    {
        var repository = new FakeAuditLogRepository { SaveFailure = new InvalidOperationException("database unavailable") };
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser(), NullLogger<AuditLogService>.Instance);

        var act = () => service.RecordAsync("OrderCancelled", "Order", Guid.NewGuid(), null, null, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_still_propagates_cancellation()
    {
        var repository = new FakeAuditLogRepository { SaveFailure = new OperationCanceledException() };
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser(), NullLogger<AuditLogService>.Instance);

        var act = () => service.RecordAsync("OrderCancelled", "Order", Guid.NewGuid(), null, null, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
