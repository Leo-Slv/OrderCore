using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Same shape as <c>EfCustomerRepository</c>: remembers which persistence
/// model backs each account it handed out and reconciles them right
/// before saving.
/// </summary>
public sealed class EfUserAccountRepository : IUserAccountRepository
{
    private static readonly string AdminRole = UserRole.Admin.ToString();

    private readonly IdentityDbContext _dbContext;
    private readonly Dictionary<Guid, (UserAccount Domain, UserAccountPersistenceModel Model)> _tracked = new();

    public EfUserAccountRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserAccount?> GetByIdAsync(Guid userAccountId, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(a => a.Id == userAccountId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<UserAccount?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(a => a.NormalizedEmail == normalizedEmail, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<UserAccount?> GetBySessionTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(a => a.Sessions.Any(s => s.TokenHash == tokenHash), cancellationToken);
        return model is null ? null : Track(model);
    }

    public Task<bool> AnyAdminAsync(CancellationToken cancellationToken) =>
        _dbContext.UserAccounts.AnyAsync(a => a.Role == AdminRole, cancellationToken);

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken)
    {
        var model = UserAccountMapper.ToPersistence(account);
        await _dbContext.UserAccounts.AddAsync(model, cancellationToken);
        _tracked[account.Id] = (account, model);
    }

    public void Remove(UserAccount account)
    {
        if (_tracked.Remove(account.Id, out var tracked))
        {
            _dbContext.UserAccounts.Remove(tracked.Model);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            UserAccountMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private UserAccount Track(UserAccountPersistenceModel model)
    {
        var domain = UserAccountMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    private IQueryable<UserAccountPersistenceModel> Query() => _dbContext.UserAccounts.Include(a => a.Sessions);
}
