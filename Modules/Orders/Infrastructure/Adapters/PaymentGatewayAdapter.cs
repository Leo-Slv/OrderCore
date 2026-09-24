using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' own <see cref="IPaymentGateway"/> by wrapping
/// Payments' already-implemented <see cref="CreatePaymentUseCase"/> and
/// <see cref="GetPaymentByOrderIdUseCase"/> — the "Application Contract"
/// indirection from section 7, same pattern as
/// <c>ProductCatalogAdapter</c>/<c>InventoryServiceAdapter</c>. Maps
/// Orders' <see cref="PaymentMethodChoice"/> to and from Payments'
/// <see cref="PaymentMethod"/>. See 05-orders.md.
/// </summary>
public sealed class PaymentGatewayAdapter : IPaymentGateway
{
    private readonly CreatePaymentUseCase _createPayment;
    private readonly GetPaymentByOrderIdUseCase _getPaymentByOrderId;

    public PaymentGatewayAdapter(CreatePaymentUseCase createPayment, GetPaymentByOrderIdUseCase getPaymentByOrderId)
    {
        _createPayment = createPayment;
        _getPaymentByOrderId = getPaymentByOrderId;
    }

    public async Task<Guid> RequestPaymentAsync(
        Guid orderId,
        decimal amount,
        string currency,
        PaymentMethodChoice method,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new CreatePaymentCommand(orderId, amount, currency, ToPaymentMethod(method), idempotencyKey);
        var result = await _createPayment.ExecuteAsync(command, cancellationToken);
        return result.PaymentId;
    }

    public async Task<OrderPaymentSummary?> GetPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _getPaymentByOrderId.ExecuteAsync(orderId, cancellationToken);

        return payment is null
            ? null
            : new OrderPaymentSummary(payment.Id, payment.Status.ToString(), ToChoice(payment.Method), payment.FailureReason);
    }

    private static PaymentMethod ToPaymentMethod(PaymentMethodChoice choice) => choice switch
    {
        PaymentMethodChoice.Card => PaymentMethod.Card,
        PaymentMethodChoice.Pix => PaymentMethod.Pix,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown payment method."),
    };

    private static PaymentMethodChoice ToChoice(PaymentMethod method) => method switch
    {
        PaymentMethod.Card => PaymentMethodChoice.Card,
        PaymentMethod.Pix => PaymentMethodChoice.Pix,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown payment method."),
    };
}
