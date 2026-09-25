using FluentAssertions;
using OrderCore.Api.Modules.AuditLogs.Application.DTOs;
using OrderCore.Api.Modules.AuditLogs.Presentation.Presenters;
using OrderCore.Api.Modules.AuditLogs.Presentation.Requests;
using OrderCore.Api.Shared.Application.DTOs;
using Xunit;

namespace OrderCore.UnitTests.AuditLogs;

public sealed class AuditLogPresenterTests
{
    [Fact]
    public void ToInput_maps_paging_and_filters()
    {
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new ListAuditLogsRequest
        {
            Page = 2,
            PageSize = 50,
            EntityName = "Order",
            EntityId = entityId,
            UserId = userId,
            Action = "OrderCancelled",
        };

        var input = AuditLogPresenter.ToInput(request);

        input.Page.Should().Be(2);
        input.PageSize.Should().Be(50);
        input.EntityName.Should().Be("Order");
        input.EntityId.Should().Be(entityId);
        input.UserId.Should().Be(userId);
        input.Action.Should().Be("OrderCancelled");
    }

    [Fact]
    public void ToResponse_maps_an_output_field_by_field()
    {
        var output = new AuditLogOutput
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Action = "OrderCreated",
            EntityName = "Order",
            EntityId = Guid.NewGuid(),
            Metadata = new Dictionary<string, string> { ["customerId"] = "abc" },
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var response = AuditLogPresenter.ToResponse(output);

        response.Id.Should().Be(output.Id);
        response.UserId.Should().Be(output.UserId);
        response.Action.Should().Be(output.Action);
        response.EntityName.Should().Be(output.EntityName);
        response.EntityId.Should().Be(output.EntityId);
        response.Metadata.Should().BeEquivalentTo(output.Metadata);
        response.CreatedAt.Should().Be(output.CreatedAt);
    }

    [Fact]
    public void ToResponse_maps_a_paged_result_preserving_pagination_fields()
    {
        var output = new AuditLogOutput
        {
            Id = Guid.NewGuid(),
            Action = "OrderCreated",
            EntityName = "Order",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var pagedResult = new PagedResult<AuditLogOutput>
        {
            Items = [output],
            Page = 1,
            PageSize = 20,
            TotalItems = 1,
            TotalPages = 1,
        };

        var response = AuditLogPresenter.ToResponse(pagedResult);

        response.Items.Should().ContainSingle();
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);
        response.TotalItems.Should().Be(1);
        response.TotalPages.Should().Be(1);
    }
}
