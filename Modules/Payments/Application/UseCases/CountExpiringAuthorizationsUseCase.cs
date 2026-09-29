using OrderCore.Api.Modules.Payments.Application.Contracts;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// How many authorized payments expire before <c>cutoff</c> — the backoffice
/// flags them so the orders ship before the money is released (Stripe spec,
/// decision 6).
/// </summary>
public sealed class CountExpiringAuthorizationsUseCase
{
    private readonly IPaymentRepository _payments;

    public CountExpiringAuthorizationsUseCase(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public Task<int> ExecuteAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        _payments.CountAuthorizationsExpiringBeforeAsync(cutoff, cancellationToken);
}
