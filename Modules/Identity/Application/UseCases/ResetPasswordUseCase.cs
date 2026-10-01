using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.Telemetry;
using OrderCore.Api.Modules.Identity.Application.Validation;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// A new password with the token from the reset e-mail (password-recovery
/// spec, item 3): same rules as sign-up; a token unknown, used or expired is
/// <c>invalid_or_expired_token</c> (400). Every session of the account ends
/// and its lockout is cleared.
/// </summary>
public sealed class ResetPasswordUseCase
{
    private readonly IUserAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _tokens;
    private readonly IAuditLogService _auditLog;
    private readonly IdentityMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public ResetPasswordUseCase(
        IUserAccountRepository accounts,
        IPasswordHasher passwordHasher,
        IRefreshTokenGenerator tokens,
        IAuditLogService auditLog,
        IdentityMetrics metrics,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        PasswordPolicy.EnsureAcceptable(newPassword);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw UserAccount.InvalidOrExpiredToken();
        }

        var tokenHash = _tokens.Hash(token);
        var account = await _accounts.GetByAccountTokenHashAsync(AccountTokenPurpose.PasswordReset, tokenHash, cancellationToken)
            ?? throw UserAccount.InvalidOrExpiredToken();

        account.ResetPassword(tokenHash, _passwordHasher.Hash(newPassword), _timeProvider.GetUtcNow());
        await _accounts.SaveChangesAsync(cancellationToken);

        _metrics.PasswordChanged("reset");
        await _auditLog.RecordAsync(
            AuditLogActionNames.PasswordReset, "UserAccount", account.Id, metadata: null, userId: account.Id, cancellationToken);
    }
}
