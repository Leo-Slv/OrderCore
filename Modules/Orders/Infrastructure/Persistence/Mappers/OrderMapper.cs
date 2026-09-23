using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between the <see cref="Order"/> aggregate and its
/// persistence models — see <c>CustomerMapper</c>'s remarks on
/// <c>ToDomain</c> going through <c>Rehydrate</c>, not <c>Create</c>.
/// <see cref="OrderItem"/> has no identity of its own, so its
/// reconciliation is keyed by ProductId directly here instead of going
/// through <c>ChildCollectionReconciler</c> (which keys by
/// <c>Entity&lt;Guid&gt;.Id</c>).
/// </summary>
public static class OrderMapper
{
    public static Order ToDomain(OrderPersistenceModel model)
    {
        var items = model.Items.Select(i => new OrderItem(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity));

        return Order.Rehydrate(
            model.Id,
            model.CustomerId,
            Enum.Parse<OrderStatus>(model.Status),
            model.Currency,
            model.CreatedAt,
            model.ConfirmedAt,
            model.CancelledAt,
            model.Version,
            items);
    }

    public static OrderPersistenceModel ToPersistence(Order domain) => new()
    {
        Id = domain.Id,
        CustomerId = domain.CustomerId,
        Status = domain.Status.ToString(),
        Currency = domain.Currency,
        CreatedAt = domain.CreatedAt,
        ConfirmedAt = domain.ConfirmedAt,
        CancelledAt = domain.CancelledAt,
        Version = domain.Version,
        Items = domain.Items.Select(ToPersistence).ToList(),
    };

    /// <summary>
    /// Applies the current state of an already-tracked <paramref name="domain"/>
    /// aggregate onto its persistence model in place — see
    /// <c>CustomerMapper.ApplyChanges</c>.
    /// </summary>
    public static void ApplyChanges(Order domain, OrderPersistenceModel model)
    {
        model.Status = domain.Status.ToString();
        model.ConfirmedAt = domain.ConfirmedAt;
        model.CancelledAt = domain.CancelledAt;

        var domainByProductId = domain.Items.ToDictionary(i => i.ProductId);

        foreach (var item in model.Items.Where(i => !domainByProductId.ContainsKey(i.ProductId)).ToList())
        {
            model.Items.Remove(item);
        }

        var modelByProductId = model.Items.ToDictionary(i => i.ProductId);

        foreach (var item in domain.Items)
        {
            if (modelByProductId.TryGetValue(item.ProductId, out var existing))
            {
                existing.Quantity = item.Quantity;
            }
            else
            {
                model.Items.Add(ToPersistence(item));
            }
        }
    }

    private static OrderItemPersistenceModel ToPersistence(OrderItem domain) => new()
    {
        ProductId = domain.ProductId,
        ProductName = domain.ProductName,
        UnitPrice = domain.UnitPrice,
        Quantity = domain.Quantity,
    };
}
