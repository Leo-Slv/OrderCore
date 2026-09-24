using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Domain.Entities;

namespace OrderCore.UnitTests.Payments;

internal sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly Dictionary<Guid, Payment> _payments = new();

    public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken) =>
        Task.FromResult(_payments.GetValueOrDefault(paymentId));

    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult(_payments.Values.FirstOrDefault(p => p.OrderId == orderId));

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        _payments[payment.Id] = payment;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
