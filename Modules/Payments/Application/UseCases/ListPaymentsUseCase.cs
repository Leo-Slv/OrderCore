using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// The backoffice payment list. Page bounds are validated the same way
/// <c>ListProductsUseCase</c> validates them.
/// </summary>
public sealed class ListPaymentsUseCase
{
    private readonly IPaymentRepository _payments;

    public ListPaymentsUseCase(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public async Task<PagedResult<Payment>> ExecuteAsync(ListPaymentsFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Page must be greater than or equal to 1.");
        }

        if (filter.PageSize is < 1 or > ListPaymentsFilter.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"PageSize must be between 1 and {ListPaymentsFilter.MaximumPageSize}.");
        }

        if (filter.CreatedFrom is { } from && filter.CreatedTo is { } to && from >= to)
        {
            throw new ArgumentException("CreatedFrom must be earlier than CreatedTo.", nameof(filter));
        }

        var (payments, totalCount) = await _payments.ListAsync(filter, cancellationToken);

        return new PagedResult<Payment>
        {
            Items = payments,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
        };
    }
}
