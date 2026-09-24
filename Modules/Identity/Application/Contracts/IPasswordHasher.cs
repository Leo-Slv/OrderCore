namespace OrderCore.Api.Modules.Identity.Application.Contracts;

public enum PasswordCheck
{
    Failed,
    Succeeded,

    /// <summary>Correct, but hashed with outdated parameters: store a fresh hash.</summary>
    SucceededRehashNeeded,
}

/// <summary>
/// Salted, slow password hashing, implemented in Infrastructure over
/// ASP.NET Core's <c>PasswordHasher&lt;T&gt;</c>.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);
}
