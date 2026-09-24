using FluentAssertions;
using OrderCore.Api.Modules.AuditLogs.Application.DTOs;
using OrderCore.Api.Modules.AuditLogs.Application.UseCases;
using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using Xunit;

namespace OrderCore.UnitTests.AuditLogs;

public sealed class ListAuditLogsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_returns_the_requested_page_and_total_count()
    {
        var repository = new FakeAuditLogRepository();
        for (var i = 0; i < 5; i++)
        {
            await repository.AddAsync(
                AuditLog.Create(null, "OrderCreated", "Order", Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddMinutes(i)),
                CancellationToken.None);
        }

        var useCase = new ListAuditLogsUseCase(repository);

        var result = await useCase.ExecuteAsync(new ListAuditLogsInput { Page = 1, PageSize = 2 }, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_page_below_one()
    {
        var useCase = new ListAuditLogsUseCase(new FakeAuditLogRepository());

        var act = () => useCase.ExecuteAsync(new ListAuditLogsInput { Page = 0, PageSize = 10 }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_page_size_above_the_maximum()
    {
        var useCase = new ListAuditLogsUseCase(new FakeAuditLogRepository());

        var act = () => useCase.ExecuteAsync(
            new ListAuditLogsInput { Page = 1, PageSize = ListAuditLogsInput.MaximumPageSize + 1 }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
