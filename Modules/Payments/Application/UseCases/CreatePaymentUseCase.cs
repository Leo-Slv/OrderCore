using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Creates a <see cref="Payment"/> and authorizes it synchronously in the
/// same call (resolved decision — see
/// Docs/specs/payments/payment-processing.md): the only registered
/// <see cref="IPaymentProvider"/> is <c>FakePaymentProvider</c>, which
/// resolves in-process with no async webhook round-trip to wait for.
/// <see cref="AuthorizePaymentUseCase"/> is the separate, explicit retry
/// path for a payment that got stuck before an outcome was recorded — this
/// use case never calls it.
/// </summary>
public sealed class CreatePaymentUseCase
{
    /// <summary>
    /// 06-payments.md's CreatePaymentCommand has no `provider` field, but
    /// `Payment.Create` requires one — same class of gap as
    /// `Customer.Create` gaining `now`. Hardcoded until a second provider
    /// (e.g. Stripe) exists and this needs to become a real choice.
    /// </summary>
    private const string Provider = "Fake";

    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly IOutboxWriter _outbox;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public CreatePaymentUseCase(
        IPaymentRepository payments, IPaymentProvider provider, IOutboxWriter outbox, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _payments = payments;
        _provider = provider;
        _outbox = outbox;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<CreatePaymentResult> ExecuteAsync(CreatePaymentCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var payment = Payment.Create(
            command.OrderId, command.Amount, command.Currency, command.Method, command.IdempotencyKey, Provider, customerPaymentMethodId: null, now);

        await _payments.AddAsync(payment, cancellationToken);

        payment.MarkProcessing();
        var result = await _provider.AuthorizeAsync(payment, cancellationToken);

        if (result.Succeeded)
        {
            payment.Authorize(result.ProviderReference!, _timeProvider.GetUtcNow());
            _outbox.Enqueue(new PaymentAuthorized
            {
                EventId = Guid.NewGuid(),
                Version = 1,
                OccurredAt = _timeProvider.GetUtcNow(),
                OrderId = payment.OrderId,
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Currency = payment.Currency,
            });
        }
        else
        {
            payment.Fail(result.FailureReason ?? "unknown_failure");
            _outbox.Enqueue(new PaymentFailed
            {
                EventId = Guid.NewGuid(),
                Version = 1,
                OccurredAt = _timeProvider.GetUtcNow(),
                OrderId = payment.OrderId,
                PaymentId = payment.Id,
                Reason = payment.FailureReason!,
            });
        }

        await _payments.SaveChangesAsync(cancellationToken);

        if (result.Succeeded)
        {
            await _auditLog.RecordAsync(
                AuditLogActionNames.PaymentAuthorized,
                "Payment",
                payment.Id,
                new Dictionary<string, string?> { ["orderId"] = payment.OrderId.ToString(), ["amount"] = payment.Amount.ToString() },
                userId: null,
                cancellationToken);
        }
        else
        {
            await _auditLog.RecordAsync(
                AuditLogActionNames.PaymentFailed,
                "Payment",
                payment.Id,
                new Dictionary<string, string?> { ["orderId"] = payment.OrderId.ToString(), ["reason"] = payment.FailureReason },
                userId: null,
                cancellationToken);
        }

        return new CreatePaymentResult(payment.Id, payment.Status.ToString());
    }
}
