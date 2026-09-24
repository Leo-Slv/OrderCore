using OrderCore.Api.Modules.Orders.Application.Contracts;

namespace OrderCore.UnitTests.Orders;

internal sealed class FakeOrderNumberGenerator : IOrderNumberGenerator
{
    private int _next = 1;

    public Task<string> NextAsync(CancellationToken cancellationToken) => Task.FromResult($"ORD-2026-{_next++:D6}");
}
