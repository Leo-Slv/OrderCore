using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Application.Validation;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// Creates a customer account and signs it in. Identity and Customers have
/// separate <c>DbContext</c>s, so this is a sequence with compensation, not
/// one transaction:
/// <list type="number">
/// <item>the account is saved first, which reserves the e-mail under its
/// unique index;</item>
/// <item>the customer is created through <see cref="ICustomerRegistry"/>.
/// If that fails, the account is deleted again, so no half-created account
/// is left behind, and the error is passed on;</item>
/// <item>the account is linked to the new customer.</item>
/// </list>
/// </summary>
public sealed class SignUpCustomerUseCase
{
    private readonly IUserAccountRepository _accounts;
    private readonly ICustomerRegistry _customers;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public SignUpCustomerUseCase(
        IUserAccountRepository accounts,
        ICustomerRegistry customers,
        IPasswordHasher passwordHasher,
        IRefreshTokenGenerator refreshTokens,
        IAccessTokenIssuer accessTokens,
        IAuditLogService auditLog,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _customers = customers;
        _passwordHasher = passwordHasher;
        _refreshTokens = refreshTokens;
        _accessTokens = accessTokens;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<AuthTokens> ExecuteAsync(SignUpCommand command, CancellationToken cancellationToken)
    {
        PasswordPolicy.EnsureAcceptable(command.Password);

        if (await _accounts.GetByNormalizedEmailAsync(UserAccount.NormalizeEmail(command.Email), cancellationToken) is not null)
        {
            throw new ConflictException("email_already_registered", "An account with this e-mail already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        var account = UserAccount.CreateCustomer(command.Email, _passwordHasher.Hash(command.Password), now);
        var refreshToken = _refreshTokens.Generate();
        var session = account.StartSession(refreshToken.Hash, now + _refreshTokens.Lifetime, now);

        await _accounts.AddAsync(account, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);

        Guid customerId;
        try
        {
            customerId = await _customers.RegisterAsync(command.Name, account.Email, command.Phone, cancellationToken);
        }
        catch
        {
            _accounts.Remove(account);
            await _accounts.SaveChangesAsync(cancellationToken);
            throw;
        }

        account.LinkCustomer(customerId);
        await _accounts.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.UserAccountCreated,
            "UserAccount",
            account.Id,
            new Dictionary<string, string?> { ["role"] = account.Role.ToString(), ["customerId"] = customerId.ToString() },
            userId: account.Id,
            cancellationToken);

        return SessionTokens.For(account, refreshToken.Token, session, _accessTokens, now);
    }
}
