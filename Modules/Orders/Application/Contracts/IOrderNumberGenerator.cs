namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Generates the human-readable identifier shown to a customer (e.g.
/// "ORD-2026-000123") — <c>Order.Id</c> (a <see cref="Guid"/>) is never
/// something a customer sees on an order confirmation. See 05-orders.md's
/// "Paridade" note.
/// </summary>
public interface IOrderNumberGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken);
}
