using FluentAssertions;
using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using Xunit;

namespace OrderCore.UnitTests.AuditLogs;

public sealed class AuditLogTests
{
    [Fact]
    public void Create_sets_all_fields_from_its_parameters()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var auditLog = AuditLog.Create(userId, "OrderCreated", "Order", entityId, "{\"customerId\":\"abc\"}", now);

        auditLog.UserId.Should().Be(userId);
        auditLog.Action.Should().Be("OrderCreated");
        auditLog.EntityName.Should().Be("Order");
        auditLog.EntityId.Should().Be(entityId);
        auditLog.MetadataJson.Should().Be("{\"customerId\":\"abc\"}");
        auditLog.CreatedAt.Should().Be(now);
        auditLog.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_allows_null_userId_entityId_and_metadata()
    {
        var auditLog = AuditLog.Create(null, "InventoryReserved", "InventoryReservation", null, null, DateTimeOffset.UtcNow);

        auditLog.UserId.Should().BeNull();
        auditLog.EntityId.Should().BeNull();
        auditLog.MetadataJson.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_action(string action)
    {
        var act = () => AuditLog.Create(null, action, "Order", null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_entity_name(string entityName)
    {
        var act = () => AuditLog.Create(null, "OrderCreated", entityName, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
