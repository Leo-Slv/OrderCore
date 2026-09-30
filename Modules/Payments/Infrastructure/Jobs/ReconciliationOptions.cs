namespace OrderCore.Api.Modules.Payments.Infrastructure.Jobs;

/// <summary>Section <c>Payments:Reconciliation</c>.</summary>
public sealed class ReconciliationOptions
{
    public const string SectionName = "Payments:Reconciliation";

    /// <summary>How often payments are checked against the provider.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>A payment still <c>Processing</c> this long after it was created may have missed a webhook.</summary>
    public TimeSpan ProcessingAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>At most this many payments per run; the rest wait for the next one.</summary>
    public int BatchSize { get; set; } = 100;
}
