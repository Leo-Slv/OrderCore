using System.Globalization;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Application.UseCases;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Adapters;

/// <summary>
/// Implements Identity's <see cref="IAccountEmails"/> over the Notifications
/// module's <see cref="QueueEmailUseCase"/> (section 7's "Application
/// Contract" indirection): builds the storefront link from the token and the
/// template values in Portuguese. Nothing here is logged — the link carries
/// the token.
/// </summary>
public sealed class AccountEmailsAdapter : IAccountEmails
{
    private readonly QueueEmailUseCase _queueEmail;
    private readonly AccountLinksOptions _links;

    public AccountEmailsAdapter(QueueEmailUseCase queueEmail, IOptions<AccountLinksOptions> links)
    {
        _queueEmail = queueEmail;
        _links = links.Value;
    }

    public Task SendPasswordResetAsync(string email, string? name, string token, TimeSpan validFor, CancellationToken cancellationToken) =>
        _queueEmail.ExecuteAsync(
            email,
            EmailTemplateNames.PasswordReset,
            new Dictionary<string, string>
            {
                ["greeting"] = Greeting(name),
                ["link"] = _links.ResetPasswordLink(token),
                ["validFor"] = Duration(validFor),
            },
            cancellationToken);

    public Task SendEmailConfirmationAsync(string email, string? name, string token, TimeSpan validFor, CancellationToken cancellationToken) =>
        _queueEmail.ExecuteAsync(
            email,
            EmailTemplateNames.EmailConfirmation,
            new Dictionary<string, string>
            {
                ["greeting"] = Greeting(name),
                ["link"] = _links.ConfirmEmailLink(token),
                ["validFor"] = Duration(validFor),
            },
            cancellationToken);

    /// <summary><c>Olá, Jane.</c> — or <c>Olá!</c> for an admin, who has no customer name.</summary>
    public static string Greeting(string? name) => string.IsNullOrWhiteSpace(name) ? "Olá!" : $"Olá, {name.Trim()}.";

    /// <summary><c>30 minutos</c>, <c>24 horas</c>, <c>1 hora</c>.</summary>
    public static string Duration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 60 || duration.TotalMinutes % 60 != 0)
        {
            var minutes = (int)Math.Round(duration.TotalMinutes);
            return string.Create(CultureInfo.InvariantCulture, $"{minutes} {(minutes == 1 ? "minuto" : "minutos")}");
        }

        var hours = (int)duration.TotalHours;
        return string.Create(CultureInfo.InvariantCulture, $"{hours} {(hours == 1 ? "hora" : "horas")}");
    }
}
