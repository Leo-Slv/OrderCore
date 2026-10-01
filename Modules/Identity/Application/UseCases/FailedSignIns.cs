using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.Telemetry;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Policies;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>
/// A wrong password, at sign-in or when changing it: counted toward the
/// lockout, and the one that reaches the limit locks the account, which is
/// audited and measured. Two wrong passwords racing on the same account can
/// collide on its version: the loser's count is dropped rather than answered
/// as a conflict, which would tell the caller the account exists.
/// </summary>
internal static class FailedSignIns
{
    public static async Task RecordAsync(
        UserAccount account,
        IUserAccountRepository accounts,
        LockoutPolicy policy,
        IAuditLogService auditLog,
        IdentityMetrics metrics,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var locked = account.RecordFailedSignIn(policy, now);
        try
        {
            await accounts.SaveChangesAsync(cancellationToken);
        }
        catch (AccountConcurrencyConflictException)
        {
            return;
        }

        if (locked)
        {
            metrics.LockedOut();
            await auditLog.RecordAsync(
                AuditLogActionNames.AccountLockedOut,
                "UserAccount",
                account.Id,
                new Dictionary<string, string?> { ["lockedOutUntil"] = account.LockedOutUntil?.ToString("O") },
                userId: null,
                cancellationToken);
        }
    }
}
