using System.Diagnostics.Metrics;

namespace OrderCore.Api.Shared.Application.Observability;

/// <summary>
/// Bucket boundaries for duration histograms recorded in seconds — the same
/// ones ASP.NET Core uses for <c>http.server.request.duration</c>. Without
/// them the SDK's default boundaries (0, 5, 10, 25… meant for milliseconds)
/// put every sub-second duration in the first bucket, and every percentile
/// comes out as the same meaningless few seconds.
/// </summary>
public static class DurationBuckets
{
    public static readonly InstrumentAdvice<double> Seconds = new()
    {
        HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
    };
}
