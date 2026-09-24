using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Domain.Entities;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

public sealed class GetPaymentByOrderIdUseCase
{
    private readonly IPaymentRepository _payments;

    public GetPaymentByOrderIdUseCase(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public Task<Payment?> ExecuteAsync(Guid orderId, CancellationToken cancellationToken) =>
        _payments.GetByOrderIdAsync(orderId, cancellationToken);
}
