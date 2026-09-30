using System.Diagnostics.Metrics;

namespace OrderCore.Api.Modules.Identity.Application.Telemetry;

/// <summary>
/// Meter <c>OrderCore.Identity</c>: accounts locked after too many wrong
/// passwords — a spike means someone is guessing. Counts only, never an
/// e-mail or account id.
/// </summary>
public sealed class IdentityMetrics
{
    public const string Name = "OrderCore.Identity";

    private readonly Counter<long> _lockouts;

    public IdentityMetrics(IMeterFactory meterFactory)
    {
        _lockouts = meterFactory.Create(Name).CreateCounter<long>(
            "ordercore.identity.lockouts", "{account}", "Accounts locked after too many wrong passwords in a row.");
    }

    public void LockedOut() => _lockouts.Add(1);
}
