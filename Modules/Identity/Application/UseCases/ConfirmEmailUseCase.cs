using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// The customer opened the link from the confirmation e-mail
/// (password-recovery spec, item 5). A token unknown, replaced or expired is
/// <c>invalid_or_expired_token</c> (400); opening the link again once
/// confirmed succeeds without changing anything. The storefront refreshes the
/// session afterwards, so the new access token says the address is confirmed
/// (decision 9).
/// </summary>
public sealed class ConfirmEmailUseCase
{
    private readonly IUserAccountRepository _accounts;
    private readonly IRefreshTokenGenerator _tokens;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public ConfirmEmailUseCase(
        IUserAccountRepository accounts, IRefreshTokenGenerator tokens, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _accounts = accounts;
        _tokens = tokens;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw UserAccount.InvalidOrExpiredToken();
        }

        var tokenHash = _tokens.Hash(token);
        var account = await _accounts.GetByAccountTokenHashAsync(AccountTokenPurpose.EmailConfirmation, tokenHash, cancellationToken)
            ?? throw UserAccount.InvalidOrExpiredToken();

        var wasConfirmed = account.EmailConfirmed;
        account.ConfirmEmail(tokenHash, _timeProvider.GetUtcNow());
        if (wasConfirmed)
        {
            return;
        }

        await _accounts.SaveChangesAsync(cancellationToken);
        await _auditLog.RecordAsync(
            AuditLogActionNames.EmailConfirmed, "UserAccount", account.Id, metadata: null, userId: account.Id, cancellationToken);
    }
}
