namespace OrderCore.Api.Modules.Payments.Infrastructure.Jobs;

/// <summary>Section <c>Payments:PaymentWindow</c> (Stripe spec, decision 8).</summary>
public sealed class PaymentWindowOptions
{
    public const string SectionName = "Payments:PaymentWindow";

    /// <summary>How long the buyer has to confirm the card after checking out.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How often the job looks for payments past their window.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>At most this many payments per check; the rest wait for the next one.</summary>
    public int BatchSize { get; set; } = 100;
}
