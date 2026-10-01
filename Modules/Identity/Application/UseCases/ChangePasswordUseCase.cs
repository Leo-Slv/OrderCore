using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.Telemetry;
using OrderCore.Api.Modules.Identity.Application.Validation;
using OrderCore.Api.Modules.Identity.Domain.Policies;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// A signed-in customer or admin replaces their password by confirming the
/// current one (password-recovery spec, item 4). The current password is
/// checked like at sign-in: a wrong one counts toward the lockout, and a
/// locked account is refused even with the right one — both answered
/// <c>invalid_current_password</c> (400, not 401: the session itself is
/// fine). Every other session ends; the one the given refresh token belongs
/// to stays.
/// </summary>
public sealed class ChangePasswordUseCase
{
    public const string InvalidCurrentPasswordCode = "invalid_current_password";

    private readonly IUserAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _tokens;
    private readonly LockoutPolicy _lockoutPolicy;
    private readonly IAuditLogService _auditLog;
    private readonly IdentityMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public ChangePasswordUseCase(
        IUserAccountRepository accounts,
        IPasswordHasher passwordHasher,
        IRefreshTokenGenerator tokens,
        LockoutPolicy lockoutPolicy,
        IAuditLogService auditLog,
        IdentityMetrics metrics,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _lockoutPolicy = lockoutPolicy;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    /// <param name="currentRefreshToken">The caller's own session, kept; null ends every session.</param>
    public async Task ExecuteAsync(
        Guid userAccountId, string currentPassword, string newPassword, string? currentRefreshToken, CancellationToken cancellationToken)
    {
        PasswordPolicy.EnsureAcceptable(newPassword);

        var account = await _accounts.GetByIdAsync(userAccountId, cancellationToken)
            ?? throw new NotFoundException("account_not_found", "The signed-in account no longer exists.");

        var check = _passwordHasher.Verify(account.PasswordHash, currentPassword ?? string.Empty);
        var now = _timeProvider.GetUtcNow();
        if (account.IsLockedOut(now))
        {
            throw InvalidCurrentPassword();
        }

        if (check == PasswordCheck.Failed)
        {
            await FailedSignIns.RecordAsync(account, _accounts, _lockoutPolicy, _auditLog, _metrics, now, cancellationToken);
            throw InvalidCurrentPassword();
        }

        var keep = string.IsNullOrWhiteSpace(currentRefreshToken) ? null : _tokens.Hash(currentRefreshToken);
        account.ChangePassword(_passwordHasher.Hash(newPassword), keep, now);
        await _accounts.SaveChangesAsync(cancellationToken);

        _metrics.PasswordChanged("change");
        await _auditLog.RecordAsync(
            AuditLogActionNames.PasswordChanged, "UserAccount", account.Id, metadata: null, userId: account.Id, cancellationToken);
    }

    private static DomainRuleViolationException InvalidCurrentPassword() =>
        new(InvalidCurrentPasswordCode, "The current password is incorrect.");
}
