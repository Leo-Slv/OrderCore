using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;

namespace OrderCore.UnitTests.Identity;

internal sealed class FakeUserAccountRepository : IUserAccountRepository
{
    private readonly Dictionary<Guid, UserAccount> _accounts = new();

    public int SaveCount { get; private set; }

    public IReadOnlyCollection<UserAccount> Accounts => _accounts.Values;

    public Task<UserAccount?> GetByIdAsync(Guid userAccountId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.GetValueOrDefault(userAccountId));

    public Task<UserAccount?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.Values.FirstOrDefault(a => a.NormalizedEmail == normalizedEmail));

    public Task<UserAccount?> GetBySessionTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.Values.FirstOrDefault(a => a.Sessions.Any(s => s.TokenHash == tokenHash)));

    public Task<bool> AnyAdminAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.Values.Any(a => a.Role == UserRole.Admin));

    public Task AddAsync(UserAccount account, CancellationToken cancellationToken)
    {
        _accounts[account.Id] = account;
        return Task.CompletedTask;
    }

    public void Remove(UserAccount account) => _accounts.Remove(account.Id);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
