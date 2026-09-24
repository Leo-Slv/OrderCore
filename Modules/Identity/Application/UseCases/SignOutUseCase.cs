using OrderCore.Api.Modules.Identity.Application.Contracts;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// Ends the sign-in the refresh token belongs to. Idempotent and silent: an
/// unknown token, an already-ended session, or a token belonging to a
/// different account than the caller's changes nothing and is not an
/// error, so the endpoint can't be used to probe tokens.
/// </summary>
public sealed class SignOutUseCase
{
    private readonly IUserAccountRepository _accounts;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly TimeProvider _timeProvider;

    public SignOutUseCase(IUserAccountRepository accounts, IRefreshTokenGenerator refreshTokens, TimeProvider timeProvider)
    {
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid userAccountId, string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = _refreshTokens.Hash(refreshToken);
        var account = await _accounts.GetBySessionTokenHashAsync(tokenHash, cancellationToken);
        if (account is null || account.Id != userAccountId)
        {
            return;
        }

        account.EndSession(tokenHash, _timeProvider.GetUtcNow());
        await _accounts.SaveChangesAsync(cancellationToken);
    }
}
