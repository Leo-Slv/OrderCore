using System.ComponentModel.DataAnnotations;
using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Payments.Presentation.Requests;

public sealed class CreatePaymentRequest
{
    public Guid OrderId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// <c>Card</c> or <c>Pix</c>. Nullable and <c>[Required]</c> so that a
    /// request without it is rejected with 400 instead of silently
    /// defaulting to the enum's first value.
    /// </summary>
    [Required]
    public PaymentMethod? Method { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;
}
