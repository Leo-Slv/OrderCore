using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.Validation;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// Creates the first administrator, and only the first: once any admin
/// exists this does nothing, so running it on every startup is safe. The
/// e-mail and password come from configuration (environment), never from
/// committed settings.
/// </summary>
public sealed class SeedAdminUseCase
{
    private readonly IUserAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public SeedAdminUseCase(
        IUserAccountRepository accounts, IPasswordHasher passwordHasher, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    /// <returns>Whether an admin was created.</returns>
    public async Task<bool> ExecuteAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (await _accounts.AnyAdminAsync(cancellationToken))
        {
            return false;
        }

        PasswordPolicy.EnsureAcceptable(password);

        if (await _accounts.GetByNormalizedEmailAsync(UserAccount.NormalizeEmail(email), cancellationToken) is not null)
        {
            throw new ConflictException("email_already_registered", "An account with the configured admin e-mail already exists.");
        }

        var admin = UserAccount.CreateAdmin(email, _passwordHasher.Hash(password), _timeProvider.GetUtcNow());
        await _accounts.AddAsync(admin, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.UserAccountCreated,
            "UserAccount",
            admin.Id,
            new Dictionary<string, string?> { ["role"] = admin.Role.ToString(), ["seeded"] = "true" },
            userId: null,
            cancellationToken);

        return true;
    }
}
