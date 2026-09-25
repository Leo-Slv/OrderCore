using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// E-mail and password in, a new session out. An unknown e-mail, a wrong
/// password, a deactivated account, a customer account whose sign-up
/// never finished and a customer an admin deactivated all fail the same
/// way (<c>invalid_credentials</c>). For
/// an unknown e-mail a password is still verified against a throwaway
/// hash, so the response time doesn't reveal which e-mails exist.
/// </summary>
public sealed class SignInUseCase
{
    public const string InvalidCredentialsCode = "invalid_credentials";

    private static readonly Lock DecoyHashLock = new();
    private static string? _decoyHash;

    private readonly IUserAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly ICustomerRegistry _customers;
    private readonly TimeProvider _timeProvider;

    public SignInUseCase(
        IUserAccountRepository accounts,
        IPasswordHasher passwordHasher,
        IRefreshTokenGenerator refreshTokens,
        IAccessTokenIssuer accessTokens,
        ICustomerRegistry customers,
        TimeProvider timeProvider)
    {
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

        var check = _passwordHasher.Verify(account.PasswordHash, command.Password);
        if (check == PasswordCheck.Failed || !account.Active || (account.Role == UserRole.Customer && account.CustomerId is null))
        {
            throw InvalidCredentials();
        }

        // Asked only once the password is right, so a wrong password costs no
        // extra call and the answer is the same either way.
        if (account.CustomerId is { } customerId && !await _customers.IsActiveAsync(customerId, cancellationToken))
        {
            throw InvalidCredentials();
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
