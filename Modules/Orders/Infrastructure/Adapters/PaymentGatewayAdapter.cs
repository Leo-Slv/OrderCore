using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' own <see cref="IPaymentGateway"/> by wrapping
/// Payments' already-implemented <see cref="CreatePaymentUseCase"/> — the
/// "Application Contract" indirection from section 7, same pattern as
/// <c>ProductCatalogAdapter</c>/<c>InventoryServiceAdapter</c>. See
/// 05-orders.md.
/// </summary>
public sealed class PaymentGatewayAdapter : IPaymentGateway
{
    private readonly CreatePaymentUseCase _createPayment;

    public PaymentGatewayAdapter(CreatePaymentUseCase createPayment)
    {
        _createPayment = createPayment;
    }

    public async Task<Guid> RequestPaymentAsync(Guid orderId, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken)
    {
        // IPaymentGateway doesn't carry the buyer's choice yet: the checkout
        // that collects it (Docs/specs/storefront, Stage 6) adds it to the
        // contract. Until then every order payment is recorded as Card, which
        // was the only method before PaymentMethod existed.
        var command = new CreatePaymentCommand(orderId, amount, currency, PaymentMethod.Card, idempotencyKey);
        var result = await _createPayment.ExecuteAsync(command, cancellationToken);
        return result.PaymentId;
    }
}
