using System.Globalization;
using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Cancels an order and leaves nothing held for it (backoffice decision 2),
/// in this order:
/// <list type="number">
/// <item>check it can be cancelled at all (not shipped, delivered or already cancelled);</item>
/// <item>settle the payment: void an authorization, refund a capture — money first,
/// so an order never ends up cancelled with the buyer's money kept;</item>
/// <item>release the stock still reserved for it;</item>
/// <item>put back on hand what it had already consumed (a confirmed order);</item>
/// <item>cancel the order and save.</item>
/// </list>
/// There is no transaction across modules, so every step before the save
/// is idempotent: if one fails, the order is left as it was and repeating
/// the cancellation finishes the job without charging, refunding or
/// returning stock twice. A payment still with the provider can't be
/// settled yet (<c>409 payment_in_progress</c>); nothing has changed then.
/// </summary>
public sealed class CancelOrderUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public CancelOrderUseCase(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        IPaymentGateway paymentGateway,
        IAuditLogService auditLog,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _paymentGateway = paymentGateway;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<OrderPaymentSettlement> ExecuteAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new ArgumentException("A reason is required to cancel an order.", nameof(command));
        }

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{command.OrderId}' was not found.");

        order.EnsureCanBeCancelled();

        var settlement = await _paymentGateway.SettleForCancellationAsync(command.OrderId, command.Reason, cancellationToken);
        await _inventoryService.ReleaseReservationsAsync(command.OrderId, cancellationToken);
        var returnedUnits = await _inventoryService.ReturnConsumedStockAsync(command.OrderId, cancellationToken);

        order.Cancel(command.Reason, _timeProvider.GetUtcNow());
        await _orderRepository.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.OrderCancelled,
            "Order",
            command.OrderId,
            new Dictionary<string, string?>
            {
                ["reason"] = command.Reason,
                ["payment"] = settlement.ToString(),
                ["returnedUnits"] = returnedUnits.ToString(CultureInfo.InvariantCulture),
            },
            userId: null,
            cancellationToken);

        return settlement;
    }
}
