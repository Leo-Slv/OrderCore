namespace OrderCore.Api.Modules.Identity.Application.Contracts;

/// <summary>
/// How Identity asks for the e-mails about an account (password-recovery
/// spec, decision 6), implemented by an adapter over the Notifications
/// module: it builds the storefront link from the token and queues the
/// e-mail, so the request never waits for the provider. The token reaches
/// the e-mail only; Identity keeps its hash.
/// </summary>
public interface IAccountEmails
{
    /// <param name="name">The customer's name; null for an admin.</param>
    Task SendPasswordResetAsync(string email, string? name, string token, TimeSpan validFor, CancellationToken cancellationToken);
}
