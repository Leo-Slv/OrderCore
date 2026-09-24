using FluentAssertions;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using Xunit;

namespace OrderCore.UnitTests.AuditLogs;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task RecordAsync_persists_an_audit_log_with_the_given_fields()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser());
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        await service.RecordAsync(
            "OrderCreated", "Order", entityId, new Dictionary<string, string?> { ["customerId"] = "abc" }, userId, CancellationToken.None);

        var stored = await repository.ListByEntityAsync("Order", entityId, CancellationToken.None);
        stored.Should().ContainSingle();
        stored.Single().Action.Should().Be("OrderCreated");
        stored.Single().UserId.Should().Be(userId);
        stored.Single().MetadataJson.Should().Contain("customerId");
    }

    [Fact]
    public async Task RecordAsync_accepts_null_metadata_and_null_userId()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser());
        var entityId = Guid.NewGuid();

        await service.RecordAsync("InventoryReserved", "InventoryReservation", entityId, null, null, CancellationToken.None);

        var stored = await repository.ListByEntityAsync("InventoryReservation", entityId, CancellationToken.None);
        stored.Single().UserId.Should().BeNull();
        stored.Single().MetadataJson.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_drops_blank_metadata_entries()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser());
        var entityId = Guid.NewGuid();

        await service.RecordAsync(
            "ProductCreated",
            "Product",
            entityId,
            new Dictionary<string, string?> { ["sku"] = "SKU-1", ["blank"] = "  ", ["nullValue"] = null },
            userId: null,
            CancellationToken.None);

        var stored = await repository.ListByEntityAsync("Product", entityId, CancellationToken.None);
        stored.Single().MetadataJson.Should().Contain("sku").And.NotContain("blank").And.NotContain("nullValue");
    }

    [Fact]
    public async Task RecordAsync_without_a_userId_records_the_signed_in_user()
    {
        var repository = new FakeAuditLogRepository();
        var signedIn = new FakeCurrentUser { UserId = Guid.NewGuid(), Role = "Admin" };
        var service = new AuditLogService(repository, TimeProvider.System, signedIn);
        var entityId = Guid.NewGuid();

        await service.RecordAsync("ProductPublished", "Product", entityId, null, null, CancellationToken.None);

        var stored = await repository.ListByEntityAsync("Product", entityId, CancellationToken.None);
        stored.Single().UserId.Should().Be(signedIn.UserId);
    }

    [Fact]
    public async Task RecordAsync_with_an_explicit_userId_keeps_it_over_the_signed_in_user()
    {
        var repository = new FakeAuditLogRepository();
        var service = new AuditLogService(repository, TimeProvider.System, new FakeCurrentUser { UserId = Guid.NewGuid() });
        var explicitUserId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        await service.RecordAsync("OrderCreated", "Order", entityId, null, explicitUserId, CancellationToken.None);

        var stored = await repository.ListByEntityAsync("Order", entityId, CancellationToken.None);
        stored.Single().UserId.Should().Be(explicitUserId);
    }
}
