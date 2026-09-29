using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Payments.Infrastructure.Webhooks;

namespace OrderCore.Api.Modules.Payments.Presentation.Controllers;

/// <summary>
/// Where Stripe sends what happened to its payments. The one anonymous
/// write endpoint: Stripe has no OrderCore token, so it is authenticated by
/// Stripe's signature of the exact body instead (checked by
/// <see cref="StripeWebhookHandler"/>), and every event is deduplicated.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("payments/webhooks/stripe")]
public sealed class StripeWebhooksController : ControllerBase
{
    private readonly StripeWebhookHandler _handler;

    public StripeWebhooksController(StripeWebhookHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// 200 once the event is handled (also for a repeated or an ignored one),
    /// 400 <c>invalid_webhook_signature</c> for a body Stripe didn't sign,
    /// 404 <c>stripe_webhooks_disabled</c> without a webhook secret, and 500
    /// on a failure, which Stripe retries.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveAsync(
        [FromHeader(Name = "Stripe-Signature")] string? signature, CancellationToken cancellationToken)
    {
        // The signature covers the exact bytes Stripe sent, so the body is read raw, never model-bound.
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        await _handler.HandleAsync(payload, signature, cancellationToken);
        return Ok();
    }
}
