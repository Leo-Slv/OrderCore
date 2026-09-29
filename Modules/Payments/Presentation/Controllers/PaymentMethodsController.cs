using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Presentation.Responses;

namespace OrderCore.Api.Modules.Payments.Presentation.Controllers;

/// <summary>
/// Anonymous: the storefront asks which payment methods to offer before
/// anyone signs in (the cart page), and nothing here is personal or secret.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("payments/methods")]
public sealed class PaymentMethodsController : ControllerBase
{
    private readonly GetAvailablePaymentMethodsUseCase _getAvailableMethods;

    public PaymentMethodsController(GetAvailablePaymentMethodsUseCase getAvailableMethods)
    {
        _getAvailableMethods = getAvailableMethods;
    }

    /// <summary>The payment methods checkout accepts now, and what the provider's card form needs.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaymentMethodsResponse), StatusCodes.Status200OK)]
    public ActionResult<PaymentMethodsResponse> Get()
    {
        var available = _getAvailableMethods.Execute();
        return Ok(new PaymentMethodsResponse(available.Provider, available.Methods.Select(m => m.ToString()).ToList(), available.PublishableKey));
    }
}
