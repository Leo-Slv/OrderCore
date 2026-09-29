namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// When the backoffice flags an order: its payment is authorized but not yet
/// captured, and the authorization expires within <see cref="WarningPeriod"/>
/// — ship it before the provider releases the money (Stripe spec, decision 6).
/// </summary>
public static class AuthorizationExpiry
{
    public static readonly TimeSpan WarningPeriod = TimeSpan.FromDays(2);

    /// <param name="paymentStatus">Payments' status name; only <c>Authorized</c> can expire.</param>
    public static bool IsExpiringSoon(string? paymentStatus, DateTimeOffset? expiresAt, DateTimeOffset now) =>
        paymentStatus == "Authorized" && expiresAt is { } at && at <= now + WarningPeriod;
}
