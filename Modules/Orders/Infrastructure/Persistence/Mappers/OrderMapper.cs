using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Domain.ValueObjects;

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
        var items = model.Items.Select(ToDomain);

        return Order.Rehydrate(
            model.Id,
            model.OrderNumber,
            model.CustomerId,
            Enum.Parse<OrderStatus>(model.Status),
            model.DiscountAmount,
            model.ShippingAmount,
            model.TaxAmount,
            model.Currency,
            ToShippingAddress(model),
            ToBillingAddress(model),
            model.CustomerNotes,
            model.InternalNotes,
            model.CreatedAt,
            model.UpdatedAt,
            model.ConfirmedAt,
            model.CancelledAt,
            model.ShippedAt,
            model.DeliveredAt,
            model.Version,
            items);
    }

    public static OrderPersistenceModel ToPersistence(Order domain)
    {
        var model = new OrderPersistenceModel
        {
            Id = domain.Id,
            OrderNumber = domain.OrderNumber,
            CustomerId = domain.CustomerId,
            Status = domain.Status.ToString(),
            DiscountAmount = domain.DiscountAmount,
            ShippingAmount = domain.ShippingAmount,
            TaxAmount = domain.TaxAmount,
            Currency = domain.Currency,
            CustomerNotes = domain.CustomerNotes,
            InternalNotes = domain.InternalNotes,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            ConfirmedAt = domain.ConfirmedAt,
            CancelledAt = domain.CancelledAt,
            ShippedAt = domain.ShippedAt,
            DeliveredAt = domain.DeliveredAt,
            Version = domain.Version,
            Items = domain.Items.Select(ToPersistence).ToList(),
        };

        ApplyAddresses(domain, model);

        return model;
    }

    /// <summary>
    /// Applies the current state of an already-tracked <paramref name="domain"/>
    /// aggregate onto its persistence model in place — see
    /// <c>CustomerMapper.ApplyChanges</c>.
    /// </summary>
    public static void ApplyChanges(Order domain, OrderPersistenceModel model)
    {
        model.Status = domain.Status.ToString();
        model.DiscountAmount = domain.DiscountAmount;
        model.ShippingAmount = domain.ShippingAmount;
        model.TaxAmount = domain.TaxAmount;
        model.CustomerNotes = domain.CustomerNotes;
        model.InternalNotes = domain.InternalNotes;
        model.UpdatedAt = domain.UpdatedAt;
        model.ConfirmedAt = domain.ConfirmedAt;
        model.CancelledAt = domain.CancelledAt;
        model.ShippedAt = domain.ShippedAt;
        model.DeliveredAt = domain.DeliveredAt;
        model.Version = domain.Version;
        ApplyAddresses(domain, model);

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
                existing.DiscountAmount = item.DiscountAmount;
            }
            else
            {
                model.Items.Add(ToPersistence(item));
            }
        }
    }

    private static void ApplyAddresses(Order domain, OrderPersistenceModel model)
    {
        model.ShippingStreet = domain.ShippingAddress?.Street;
        model.ShippingNumber = domain.ShippingAddress?.Number;
        model.ShippingComplement = domain.ShippingAddress?.Complement;
        model.ShippingNeighborhood = domain.ShippingAddress?.Neighborhood;
        model.ShippingCity = domain.ShippingAddress?.City;
        model.ShippingState = domain.ShippingAddress?.State;
        model.ShippingPostalCode = domain.ShippingAddress?.PostalCode;
        model.ShippingCountry = domain.ShippingAddress?.Country;

        model.BillingStreet = domain.BillingAddress?.Street;
        model.BillingNumber = domain.BillingAddress?.Number;
        model.BillingComplement = domain.BillingAddress?.Complement;
        model.BillingNeighborhood = domain.BillingAddress?.Neighborhood;
        model.BillingCity = domain.BillingAddress?.City;
        model.BillingState = domain.BillingAddress?.State;
        model.BillingPostalCode = domain.BillingAddress?.PostalCode;
        model.BillingCountry = domain.BillingAddress?.Country;
    }

    private static Address? ToShippingAddress(OrderPersistenceModel model) =>
        model.ShippingStreet is null
            ? null
            : Address.Create(
                model.ShippingStreet, model.ShippingNumber!, model.ShippingComplement, model.ShippingNeighborhood!,
                model.ShippingCity!, model.ShippingState!, model.ShippingPostalCode!, model.ShippingCountry!);

    private static Address? ToBillingAddress(OrderPersistenceModel model) =>
        model.BillingStreet is null
            ? null
            : Address.Create(
                model.BillingStreet, model.BillingNumber!, model.BillingComplement, model.BillingNeighborhood!,
                model.BillingCity!, model.BillingState!, model.BillingPostalCode!, model.BillingCountry!);

    private static OrderItemPersistenceModel ToPersistence(OrderItem domain) => new()
    {
        ProductId = domain.ProductId,
        ProductVariantId = domain.ProductVariantId,
        ProductSku = domain.ProductSku,
        ProductName = domain.ProductName,
        ProductImageUrl = domain.ProductImageUrl,
        UnitPrice = domain.UnitPrice,
        Quantity = domain.Quantity,
        DiscountAmount = domain.DiscountAmount,
    };

    private static OrderItem ToDomain(OrderItemPersistenceModel model)
    {
        var item = new OrderItem(
            model.ProductId, model.ProductVariantId, model.ProductSku, model.ProductName, model.ProductImageUrl, model.UnitPrice, model.Quantity);

        if (model.DiscountAmount > 0)
        {
            item.ApplyDiscount(model.DiscountAmount);
        }

        return item;
    }
}
