using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

public sealed class GetPaymentByIdUseCase
{
    private readonly IPaymentRepository _payments;

    public GetPaymentByIdUseCase(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public async Task<Payment> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken) =>
        await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException("payment_not_found", $"Payment '{paymentId}' was not found.");
}
