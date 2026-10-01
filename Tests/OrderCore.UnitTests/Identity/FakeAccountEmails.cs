using OrderCore.Api.Modules.Identity.Application.Contracts;

namespace OrderCore.UnitTests.Identity;

/// <summary>Records the account e-mails asked for, with their tokens, instead of queuing them.</summary>
internal sealed class FakeAccountEmails : IAccountEmails
{
    public List<(string Email, string? Name, string Token, TimeSpan ValidFor)> PasswordResets { get; } = [];

    public List<(string Email, string? Name, string Token, TimeSpan ValidFor)> EmailConfirmations { get; } = [];

    /// <summary>Makes queuing fail, as a database outage would.</summary>
    public Exception? FailWith { get; set; }

    public Task SendPasswordResetAsync(string email, string? name, string token, TimeSpan validFor, CancellationToken cancellationToken)
    {
        PasswordResets.Add((email, name, token, validFor));
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string email, string? name, string token, TimeSpan validFor, CancellationToken cancellationToken)
    {
        if (FailWith is { } failure)
        {
            throw failure;
        }

        EmailConfirmations.Add((email, name, token, validFor));
        return Task.CompletedTask;
    }
}
