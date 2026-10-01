using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.Telemetry;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// "Forgot my password" (password-recovery spec, item 2), for customers and
/// admins. Answers the same way whether the address has an account or not,
/// so the form can't tell who has one: nothing is thrown either way. For an
/// active account, a single-use token valid for <see cref="TokenLifetime"/>
/// replaces any earlier one, and the e-mail with the link is queued.
/// </summary>
public sealed class RequestPasswordResetUseCase
{
    /// <summary>Spec decision 2.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private readonly IUserAccountRepository _accounts;
    private readonly IRefreshTokenGenerator _tokens;
    private readonly ICustomerRegistry _customers;
    private readonly IAccountEmails _emails;
    private readonly IAuditLogService _auditLog;
    private readonly IdentityMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public RequestPasswordResetUseCase(
        IUserAccountRepository accounts,
        IRefreshTokenGenerator tokens,
        ICustomerRegistry customers,
        IAccountEmails emails,
        IAuditLogService auditLog,
        IdentityMetrics metrics,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _tokens = tokens;
        _customers = customers;
        _emails = emails;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(string email, CancellationToken cancellationToken)
    {
        var account = string.IsNullOrWhiteSpace(email)
            ? null
            : await _accounts.GetByNormalizedEmailAsync(UserAccount.NormalizeEmail(email), cancellationToken);
        if (account is null || !account.Active || (account.Role == UserRole.Customer && account.CustomerId is null))
        {
            _metrics.PasswordResetRequested("ignored");
            return;
        }

        var token = _tokens.Generate();
        account.IssueToken(AccountTokenPurpose.PasswordReset, token.Hash, TokenLifetime, _timeProvider.GetUtcNow());
        try
        {
            await _accounts.SaveChangesAsync(cancellationToken);
        }
        catch (AccountConcurrencyConflictException)
        {
            // Two requests at once: the other one's link goes out. Answering a
            // conflict would tell the caller the account exists.
            return;
        }

        var name = account.CustomerId is { } customerId ? await _customers.GetNameAsync(customerId, cancellationToken) : null;
        await _emails.SendPasswordResetAsync(account.Email, name, token.Token, TokenLifetime, cancellationToken);

        _metrics.PasswordResetRequested("sent");
        await _auditLog.RecordAsync(
            AuditLogActionNames.PasswordResetRequested, "UserAccount", account.Id, metadata: null, userId: account.Id, cancellationToken);
    }
}
