using System.ComponentModel.DataAnnotations;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

public sealed class RequestOrderPaymentRequest
{
    /// <summary><c>Card</c> or <c>Pix</c>.</summary>
    [Required]
    public PaymentMethodChoice? PaymentMethod { get; init; }
}
