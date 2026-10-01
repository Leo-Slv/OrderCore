using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Application.Telemetry;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Modules.Identity.Domain.Policies;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// E-mail and password in, a new session out. An unknown e-mail, a wrong
/// password, a deactivated account and a customer account whose sign-up
/// never finished all fail the same way (<c>invalid_credentials</c>). A
/// customer an admin deactivated gets <c>account_inactive</c> instead, so
/// the storefront can say why — but only once the password is right, so
/// it still reveals nothing to someone who doesn't know it. For
/// an unknown e-mail a password is still verified against a throwaway
/// hash, so the response time doesn't reveal which e-mails exist.
/// <para>
/// Wrong passwords in a row lock the account for a while
/// (<see cref="LockoutPolicy"/>, production-readiness spec, decision 4). A
/// locked account answers exactly like a wrong password — even with the right
/// one — so a lock tells nothing about which accounts exist; the next
/// successful sign-in after it runs out resets the count. Sessions already
/// open keep working: the lock is about guessing passwords.
/// </para>
/// </summary>
public sealed class SignInUseCase
{
    public const string InvalidCredentialsCode = "invalid_credentials";
    public const string AccountInactiveCode = "account_inactive";

    private static readonly Lock DecoyHashLock = new();
    private static string? _decoyHash;

    private readonly IUserAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly ICustomerRegistry _customers;
    private readonly TimeProvider _timeProvider;
    private readonly LockoutPolicy _lockoutPolicy;
    private readonly IAuditLogService _auditLog;
    private readonly IdentityMetrics _metrics;

    public SignInUseCase(
        IUserAccountRepository accounts,
        IPasswordHasher passwordHasher,
        IRefreshTokenGenerator refreshTokens,
        IAccessTokenIssuer accessTokens,
        ICustomerRegistry customers,
        TimeProvider timeProvider,
        LockoutPolicy lockoutPolicy,
        IAuditLogService auditLog,
        IdentityMetrics metrics)
    {
        _lockoutPolicy = lockoutPolicy;
        _auditLog = auditLog;
        _metrics = metrics;
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _refreshTokens = refreshTokens;
        _accessTokens = accessTokens;
        _customers = customers;
        _timeProvider = timeProvider;
    }

    public async Task<AuthTokens> ExecuteAsync(SignInCommand command, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByNormalizedEmailAsync(UserAccount.NormalizeEmail(command.Email), cancellationToken);
        if (account is null)
        {
            _passwordHasher.Verify(DecoyHash(), command.Password);
            throw InvalidCredentials();
        }

        // The password is verified even for a locked account, so a lock doesn't
        // answer faster than a wrong password.
        var check = _passwordHasher.Verify(account.PasswordHash, command.Password);
        var attemptAt = _timeProvider.GetUtcNow();
        if (account.IsLockedOut(attemptAt))
        {
            throw InvalidCredentials();
        }

        if (check == PasswordCheck.Failed)
        {
            await FailedSignIns.RecordAsync(account, _accounts, _lockoutPolicy, _auditLog, _metrics, attemptAt, cancellationToken);
            throw InvalidCredentials();
        }

        if (!account.Active || (account.Role == UserRole.Customer && account.CustomerId is null))
        {
            throw InvalidCredentials();
        }

        // Asked only once the password is right: a wrong password costs no
        // extra call, and only the account's owner learns it is deactivated.
        if (account.CustomerId is { } customerId && !await _customers.IsActiveAsync(customerId, cancellationToken))
        {
            throw new UnauthorizedException(
                AccountInactiveCode, "This account was deactivated by the store. Contact support to reactivate it.");
        }

        var now = _timeProvider.GetUtcNow();
        if (check == PasswordCheck.SucceededRehashNeeded)
        {
            account.ReplacePasswordHash(_passwordHasher.Hash(command.Password), now);
        }

        var refreshToken = _refreshTokens.Generate();
        var session = account.StartSession(refreshToken.Hash, now + _refreshTokens.Lifetime, now);
        await _accounts.SaveChangesAsync(cancellationToken);

        return SessionTokens.For(account, refreshToken.Token, session, _accessTokens, now);
    }

    private static UnauthorizedException InvalidCredentials() =>
        new(InvalidCredentialsCode, "The e-mail or password is incorrect.");

    private string DecoyHash()
    {
        lock (DecoyHashLock)
        {
            return _decoyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString());
        }
    }
}
