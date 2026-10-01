using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// Sends the e-mail-confirmation link (password-recovery spec, item 5): at
/// sign-up, and again whenever the signed-in customer asks. Each new link
/// lasts <see cref="TokenLifetime"/> and replaces the previous one. An
/// address already confirmed is <c>email_already_confirmed</c> (409).
/// </summary>
public sealed class RequestEmailConfirmationUseCase
{
    public const string AlreadyConfirmedCode = "email_already_confirmed";

    /// <summary>Spec decision 8.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    private readonly IUserAccountRepository _accounts;
    private readonly IRefreshTokenGenerator _tokens;
    private readonly ICustomerRegistry _customers;
    private readonly IAccountEmails _emails;
    private readonly TimeProvider _timeProvider;

    public RequestEmailConfirmationUseCase(
        IUserAccountRepository accounts,
        IRefreshTokenGenerator tokens,
        ICustomerRegistry customers,
        IAccountEmails emails,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _tokens = tokens;
        _customers = customers;
        _emails = emails;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid userAccountId, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByIdAsync(userAccountId, cancellationToken)
            ?? throw new NotFoundException("account_not_found", "The signed-in account no longer exists.");
        if (account.EmailConfirmed)
        {
            throw new ConflictException(AlreadyConfirmedCode, "This e-mail address is already confirmed.");
        }

        var name = account.CustomerId is { } customerId ? await _customers.GetNameAsync(customerId, cancellationToken) : null;
        await SendAsync(account, name, cancellationToken);
    }

    /// <summary>Issues a new link on <paramref name="account"/>, saves it and queues the e-mail.</summary>
    internal async Task SendAsync(UserAccount account, string? name, CancellationToken cancellationToken)
    {
        var token = _tokens.Generate();
        account.IssueToken(AccountTokenPurpose.EmailConfirmation, token.Hash, TokenLifetime, _timeProvider.GetUtcNow());
        await _accounts.SaveChangesAsync(cancellationToken);
        await _emails.SendEmailConfirmationAsync(account.Email, name, token.Token, TokenLifetime, cancellationToken);
    }
}
