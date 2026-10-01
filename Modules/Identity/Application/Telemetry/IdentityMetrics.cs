using System.Diagnostics.Metrics;

namespace OrderCore.Api.Modules.Identity.Application.Telemetry;

/// <summary>
/// Meter <c>OrderCore.Identity</c>: accounts locked after too many wrong
/// passwords — a spike means someone is guessing — and password resets
/// asked for and passwords changed. Counts only, never an e-mail or
/// account id.
/// </summary>
public sealed class IdentityMetrics
{
    public const string Name = "OrderCore.Identity";

    private readonly Counter<long> _lockouts;
    private readonly Counter<long> _resetRequests;
    private readonly Counter<long> _passwordChanges;

    public IdentityMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Name);
        _lockouts = meter.CreateCounter<long>(
            "ordercore.identity.lockouts", "{account}", "Accounts locked after too many wrong passwords in a row.");
        _resetRequests = meter.CreateCounter<long>(
            "ordercore.identity.password_reset_requests",
            "{request}",
            "Password resets asked for, by whether the address has an account (sent) or not (ignored).");
        _passwordChanges = meter.CreateCounter<long>(
            "ordercore.identity.password_changes", "{account}", "Passwords replaced, by how: reset (by e-mail) or change (signed in).");
    }

    public void LockedOut() => _lockouts.Add(1);

    /// <param name="outcome"><c>sent</c> or <c>ignored</c>.</param>
    public void PasswordResetRequested(string outcome) =>
        _resetRequests.Add(1, new KeyValuePair<string, object?>("ordercore.outcome", outcome));

    /// <param name="how"><c>reset</c> or <c>change</c>.</param>
    public void PasswordChanged(string how) =>
        _passwordChanges.Add(1, new KeyValuePair<string, object?>("ordercore.reason", how));
}
