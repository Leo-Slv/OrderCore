using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using Stripe;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Webhooks;

public enum StripeWebhookOutcome
{
    Handled,

    /// <summary>Already handled: Stripe delivered the same event again.</summary>
    Duplicate,

    /// <summary>An event type OrderCore doesn't act on.</summary>
    NotHandled,
}

/// <summary>
/// Receives Stripe's webhooks (Docs/specs/payments/stripe-provider.md) —
/// an inbound adapter, like the integration-event handlers:
/// <list type="bullet">
/// <item>verifies the <c>Stripe-Signature</c> header against the webhook
/// secret, timestamp tolerance included (<c>400 invalid_webhook_signature</c>
/// otherwise — anyone can reach the endpoint, only Stripe can sign);</item>
/// <item>deduplicates by Stripe's event id in Payments' inbox (consumer
/// <see cref="Consumer"/>), committed with the payment's changes, so a
/// redelivered event changes nothing;</item>
/// <item>translates the event into a provider-neutral
/// <see cref="PaymentProviderUpdate"/> for
/// <see cref="ApplyPaymentProviderUpdateUseCase"/>, which knows what each
/// one means for the payment.</item>
/// </list>
/// A failure throws, so the endpoint answers 500 and Stripe retries.
/// </summary>
public sealed class StripeWebhookHandler
{
    public const string Consumer = "stripe-webhooks";

    private readonly StripeOptions _options;
    private readonly PaymentsDbContext _dbContext;
    private readonly ApplyPaymentProviderUpdateUseCase _applyUpdate;
    private readonly PaymentsMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public StripeWebhookHandler(
        IOptions<StripeOptions> options,
        PaymentsDbContext dbContext,
        ApplyPaymentProviderUpdateUseCase applyUpdate,
        PaymentsMetrics metrics,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _applyUpdate = applyUpdate;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task<StripeWebhookOutcome> HandleAsync(string payload, string? signature, CancellationToken cancellationToken)
    {
        var stripeEvent = Verify(payload, signature);

        var update = Translate(stripeEvent);
        if (update is null)
        {
            return StripeWebhookOutcome.NotHandled;
        }

        var messageId = MessageIdOf(stripeEvent.Id);
        var inbox = _dbContext.Set<InboxMessage>();
        if (await inbox.AsNoTracking().AnyAsync(m => m.MessageId == messageId && m.Consumer == Consumer, cancellationToken))
        {
            _metrics.ProviderUpdate(ApplyPaymentProviderUpdateUseCase.KindTag(update.Kind), "duplicate");
            return StripeWebhookOutcome.Duplicate;
        }

        inbox.Add(new InboxMessage { MessageId = messageId, Consumer = Consumer, ProcessedAt = _timeProvider.GetUtcNow() });
        try
        {
            // The update saves the inbox row with the payment's changes; when it
            // changes nothing, this save records the event as handled on its own.
            await _applyUpdate.ExecuteAsync(update, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsInboxConflict(exception))
        {
            return StripeWebhookOutcome.Duplicate;
        }

        return StripeWebhookOutcome.Handled;
    }

    private Event Verify(string payload, string? signature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            throw new NotFoundException("stripe_webhooks_disabled", "Stripe webhooks are not configured.");
        }

        try
        {
            return EventUtility.ConstructEvent(payload, signature ?? string.Empty, _options.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            throw new DomainRuleViolationException("invalid_webhook_signature", "The webhook's Stripe-Signature is missing, wrong or too old.");
        }
    }

    /// <summary>The Stripe events OrderCore acts on, in its own terms; null for any other.</summary>
    private static PaymentProviderUpdate? Translate(Event stripeEvent)
    {
        var occurredAt = new DateTimeOffset(DateTime.SpecifyKind(stripeEvent.Created, DateTimeKind.Utc));

        return (stripeEvent.Type, stripeEvent.Data.Object) switch
        {
            (EventTypes.PaymentIntentAmountCapturableUpdated, PaymentIntent intent) =>
                new(PaymentProviderUpdateKind.Authorized, intent.Id, occurredAt),
            (EventTypes.PaymentIntentPaymentFailed, PaymentIntent intent) =>
                new(PaymentProviderUpdateKind.Declined, intent.Id, occurredAt,
                    Reason: intent.LastPaymentError?.DeclineCode ?? intent.LastPaymentError?.Code ?? "payment_failed"),
            (EventTypes.PaymentIntentSucceeded, PaymentIntent intent) =>
                new(PaymentProviderUpdateKind.Captured, intent.Id, occurredAt),

            // Stripe cancels an uncaptured authorization by itself ("automatic") once it expires.
            (EventTypes.PaymentIntentCanceled, PaymentIntent intent) =>
                new(PaymentProviderUpdateKind.Canceled, intent.Id, occurredAt, AuthorizationExpired: intent.CancellationReason == "automatic"),

            (EventTypes.ChargeRefundUpdated, Refund refund) when RefundUpdate(refund, occurredAt) is { } refundUpdate => refundUpdate,
            (EventTypes.ChargeDisputeCreated, Dispute dispute) when dispute.PaymentIntentId is not null =>
                new(PaymentProviderUpdateKind.DisputeOpened, dispute.PaymentIntentId, occurredAt, Reason: dispute.Reason),
            _ => null,
        };
    }

    /// <summary>Only a refund OrderCore requested (its id in the metadata) that has settled.</summary>
    private static PaymentProviderUpdate? RefundUpdate(Refund refund, DateTimeOffset occurredAt)
    {
        if (refund.PaymentIntentId is null
            || refund.Metadata?.GetValueOrDefault("refund_id") is not { } id
            || !Guid.TryParse(id, out var refundId))
        {
            return null;
        }

        return refund.Status switch
        {
            "succeeded" => new(PaymentProviderUpdateKind.RefundSucceeded, refund.PaymentIntentId, occurredAt, RefundId: refundId),
            "failed" or "canceled" => new(
                PaymentProviderUpdateKind.RefundFailed, refund.PaymentIntentId, occurredAt, Reason: refund.FailureReason ?? "refund_failed", RefundId: refundId),
            _ => null,
        };
    }

    /// <summary>The inbox keys by Guid; Stripe's event ids (<c>evt_…</c>) map to one deterministically.</summary>
    public static Guid MessageIdOf(string stripeEventId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"stripe:{stripeEventId}")).AsSpan(0, 16));

    private static bool IsInboxConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        && exception.Entries.Any(e => e.Entity is InboxMessage);
}
