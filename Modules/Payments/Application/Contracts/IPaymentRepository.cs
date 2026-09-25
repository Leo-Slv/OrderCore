using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Entities;

namespace OrderCore.Api.Modules.Payments.Application.Contracts;

/// <summary>
/// Persistence abstraction the Application layer depends on. Keeps its own
/// `SaveChangesAsync` (unlike Inventory's repositories): every Payments use
/// case only ever mutates one `Payment` aggregate root per operation, so
/// there's no cross-aggregate atomicity problem requiring an
/// `IUnitOfWork` — same shape as `IOrderRepository`/`ICustomerRepository`.
/// </summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Read-only; orders without a payment are simply absent.</summary>
    Task<IReadOnlyList<Payment>> ListByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken);

    /// <summary>One page of payments, newest first, plus how many match in total.</summary>
    Task<(IReadOnlyList<Payment> Items, int TotalCount)> ListAsync(ListPaymentsFilter filter, CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
