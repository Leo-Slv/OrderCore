using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>Staff-only notes on an order, in any status; blank clears them.</summary>
public sealed class SetOrderInternalNotesUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAuditLogService _auditLog;

    public SetOrderInternalNotesUseCase(IOrderRepository orderRepository, IAuditLogService auditLog)
    {
        _orderRepository = orderRepository;
        _auditLog = auditLog;
    }

    public async Task ExecuteAsync(Guid orderId, string? notes, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        order.SetInternalNotes(notes);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.OrderInternalNotesChanged, "Order", orderId, metadata: null, userId: null, cancellationToken);
    }
}
