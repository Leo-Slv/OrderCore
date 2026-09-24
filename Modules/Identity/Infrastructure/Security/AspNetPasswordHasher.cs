using Microsoft.AspNetCore.Identity;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Security;

/// <summary>
/// <see cref="IPasswordHasher"/> over ASP.NET Core's
/// <see cref="PasswordHasher{TUser}"/>: PBKDF2 with a per-password salt, and
/// a versioned format, so hashes made with older parameters are reported as
/// "rehash needed" instead of failing. Part of the shared framework, not an
/// ASP.NET Core Identity dependency (no Identity stores or tables are used).
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(user: null!, password);

    public PasswordCheck Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(user: null!, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Succeeded,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SucceededRehashNeeded,
            _ => PasswordCheck.Failed,
        };
}
