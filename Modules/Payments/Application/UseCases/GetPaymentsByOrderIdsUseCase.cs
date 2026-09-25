using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Domain.Entities;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// The payments of several orders in one query, for a page of the
/// backoffice order list (through Orders' <c>PaymentGatewayAdapter</c>).
/// Orders without a payment are simply absent.
/// </summary>
public sealed class GetPaymentsByOrderIdsUseCase
{
    private readonly IPaymentRepository _payments;

    public GetPaymentsByOrderIdsUseCase(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public async Task<IReadOnlyList<Payment>> ExecuteAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        var distinctIds = orderIds.Distinct().ToList();
        return distinctIds.Count == 0 ? [] : await _payments.ListByOrderIdsAsync(distinctIds, cancellationToken);
    }
}
