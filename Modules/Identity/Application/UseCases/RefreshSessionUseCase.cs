using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// Exchanges a refresh token for a new access token and a new refresh
/// token; the presented one stops working. Presenting a token that was
/// already rotated is treated as theft: the domain revokes the whole
/// session family, which is saved (and audited) before the request is
/// refused. Every failure is <c>invalid_refresh_token</c> for the client.
/// </summary>
public sealed class RefreshSessionUseCase
{
    public const string InvalidRefreshTokenCode = "invalid_refresh_token";

    private readonly IUserAccountRepository _accounts;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public RefreshSessionUseCase(
        IUserAccountRepository accounts,
        IRefreshTokenGenerator refreshTokens,
        IAccessTokenIssuer accessTokens,
        IAuditLogService auditLog,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _accessTokens = accessTokens;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<AuthTokens> ExecuteAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw InvalidRefreshToken();
        }

        var presentedHash = _refreshTokens.Hash(refreshToken);
        var account = await _accounts.GetBySessionTokenHashAsync(presentedHash, cancellationToken)
            ?? throw InvalidRefreshToken();

        var now = _timeProvider.GetUtcNow();
        var replacement = _refreshTokens.Generate();
        var (outcome, session) = account.RotateSession(presentedHash, replacement.Hash, now + _refreshTokens.Lifetime, now);

        if (outcome == SessionRotationOutcome.Reused)
        {
            await _accounts.SaveChangesAsync(cancellationToken);
            await _auditLog.RecordAsync(
                AuditLogActionNames.RefreshTokenReuseDetected,
                "UserAccount",
                account.Id,
                metadata: null,
                userId: account.Id,
                cancellationToken);
        }

        if (outcome != SessionRotationOutcome.Rotated)
        {
            throw InvalidRefreshToken();
        }

        await _accounts.SaveChangesAsync(cancellationToken);

        return SessionTokens.For(account, replacement.Token, session!, _accessTokens, now);
    }

    private static UnauthorizedException InvalidRefreshToken() =>
        new(InvalidRefreshTokenCode, "The refresh token is invalid or has expired. Sign in again.");
}
