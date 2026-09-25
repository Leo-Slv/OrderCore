using FluentAssertions;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using Xunit;

namespace OrderCore.UnitTests.Payments;

public sealed class ListPaymentsUseCaseTests
{
    private readonly ListPaymentsUseCase _useCase = new(new FakePaymentRepository());

    [Fact]
    public async Task A_page_below_one_is_rejected()
    {
        var act = () => _useCase.ExecuteAsync(new ListPaymentsFilter { Page = 0 }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task A_page_size_above_the_maximum_is_rejected()
    {
        var act = () => _useCase.ExecuteAsync(
            new ListPaymentsFilter { PageSize = ListPaymentsFilter.MaximumPageSize + 1 }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task A_created_range_that_ends_before_it_starts_is_rejected()
    {
        var now = DateTimeOffset.UtcNow;

        var act = () => _useCase.ExecuteAsync(
            new ListPaymentsFilter { CreatedFrom = now, CreatedTo = now.AddDays(-1) }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
