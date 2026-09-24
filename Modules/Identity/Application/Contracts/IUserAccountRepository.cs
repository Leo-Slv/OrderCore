using OrderCore.Api.Modules.Identity.Domain.Entities;

namespace OrderCore.Api.Modules.Identity.Application.Contracts;

/// <summary>
/// Persistence abstraction for <see cref="UserAccount"/>, same shape as the
/// other modules' repositories: no explicit update, changes to accounts it
/// handed out are saved by <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid userAccountId, CancellationToken cancellationToken);

    Task<UserAccount?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>The account owning the refresh session with this token hash, sessions included.</summary>
    Task<UserAccount?> GetBySessionTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<bool> AnyAdminAsync(CancellationToken cancellationToken);

    Task AddAsync(UserAccount account, CancellationToken cancellationToken);

    /// <summary>Only for compensating a sign-up whose customer could not be created.</summary>
    void Remove(UserAccount account);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
