using System.ComponentModel.DataAnnotations;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

/// <summary>
/// The idempotency key is not part of the body: it goes in the
/// <c>Idempotency-Key</c> header. <see cref="ExpectedTotal"/> is optional:
/// send the total the buyer was shown to have the checkout refuse
/// (<c>409 price_changed</c>) if prices moved since.
/// </summary>
public sealed class CheckoutRequest
{
    public Guid CustomerId { get; init; }

    public IReadOnlyList<CheckoutItemRequest> Items { get; init; } = [];

    public Guid ShippingAddressId { get; init; }

    public Guid BillingAddressId { get; init; }

    /// <summary>
    /// <c>Card</c> or <c>Pix</c>. Nullable and <c>[Required]</c> so that
    /// leaving it out is a 400 instead of silently meaning <c>Card</c>.
    /// </summary>
    [Required]
    public PaymentMethodChoice? PaymentMethod { get; init; }

    [MaxLength(1000)]
    public string? CustomerNotes { get; init; }

    public decimal? ExpectedTotal { get; init; }
}
