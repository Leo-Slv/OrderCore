namespace OrderCore.Api.Modules.Orders.Domain.ValueObjects;

/// <summary>
/// How a shipped order can be followed outside the store
/// (Docs/specs/tracking/realtime-order-tracking.md, decision 3): the carrier,
/// its tracking code and a link to the carrier's tracking page. Everything is
/// optional — an admin may ship without any of it — but a tracking code means
/// nothing without the carrier it belongs to, and the link must be a web
/// address the customer can open.
/// </summary>
public sealed record ShipmentDetails
{
    public const int MaxCarrierLength = 100;
    public const int MaxTrackingCodeLength = 100;
    public const int MaxTrackingUrlLength = 2000;

    public string? Carrier { get; }

    public string? TrackingCode { get; }

    public string? TrackingUrl { get; }

    private ShipmentDetails(string? carrier, string? trackingCode, string? trackingUrl)
    {
        Carrier = carrier;
        TrackingCode = trackingCode;
        TrackingUrl = trackingUrl;
    }

    /// <returns>The details, or <c>null</c> when none was given.</returns>
    public static ShipmentDetails? Create(string? carrier, string? trackingCode, string? trackingUrl)
    {
        carrier = Clean(carrier);
        trackingCode = Clean(trackingCode);
        trackingUrl = Clean(trackingUrl);

        if (carrier is null && trackingCode is null && trackingUrl is null)
        {
            return null;
        }

        if (carrier?.Length > MaxCarrierLength)
        {
            throw new ArgumentException($"The carrier can have at most {MaxCarrierLength} characters.", nameof(carrier));
        }

        if (trackingCode?.Length > MaxTrackingCodeLength)
        {
            throw new ArgumentException($"The tracking code can have at most {MaxTrackingCodeLength} characters.", nameof(trackingCode));
        }

        if (trackingCode is not null && carrier is null)
        {
            throw new ArgumentException("A tracking code needs the carrier it belongs to.", nameof(carrier));
        }

        if (trackingUrl is not null
            && (trackingUrl.Length > MaxTrackingUrlLength
                || !Uri.TryCreate(trackingUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException(
                $"The tracking link must be an absolute http(s) address of at most {MaxTrackingUrlLength} characters.", nameof(trackingUrl));
        }

        return new ShipmentDetails(carrier, trackingCode, trackingUrl);
    }

    /// <summary>Rebuilds stored details without validating them again; <c>null</c> when none were stored.</summary>
    internal static ShipmentDetails? Rehydrate(string? carrier, string? trackingCode, string? trackingUrl) =>
        carrier is null && trackingCode is null && trackingUrl is null ? null : new ShipmentDetails(carrier, trackingCode, trackingUrl);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
