namespace Blow.AI.Mind.Injections;

/// <summary>
/// Helpers for beacon URLs: unique links planted by <c>{{beacon}}</c> injections.
/// A request to such a URL on your own host shows that an agent acted on the instruction instead of only quoting it.
/// </summary>
public static class Beacon
{
    /// <summary>Validates a beacon base address: absolute <c>http</c>/<c>https</c>, without query or fragment.</summary>
    /// <exception cref="ArgumentException">The address is not a valid beacon base.</exception>
    public static Uri NormalizeBase(Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Beacon base must be an absolute http or https URL.", nameof(baseUri));
        }

        if (!string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment) || !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            throw new ArgumentException("Beacon base must not contain user info, a query or a fragment.", nameof(baseUri));
        }

        var text = baseUri.GetLeftPart(UriPartial.Path);
        return new Uri(text.EndsWith('/') ? text : text + "/");
    }

    /// <summary>Builds the beacon URL for <paramref name="marker"/>: <c>&lt;base&gt;/&lt;marker&gt;</c>.</summary>
    public static Uri CreateUrl(Uri baseUri, string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        return new Uri(NormalizeBase(baseUri), Uri.EscapeDataString(marker));
    }

    /// <summary>
    /// Extracts the marker from a request to a beacon URL, e.g. inside your endpoint handler.
    /// Returns <c>false</c> when <paramref name="requestUri"/> is not a beacon URL below <paramref name="baseUri"/>.
    /// </summary>
    public static bool TryGetMarker(Uri requestUri, Uri baseUri, out string marker)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        marker = string.Empty;
        var root = NormalizeBase(baseUri);
        if (!requestUri.IsAbsoluteUri || !root.IsBaseOf(requestUri))
        {
            return false;
        }

        var relative = Uri.UnescapeDataString(root.MakeRelativeUri(new Uri(requestUri.GetLeftPart(UriPartial.Path))).OriginalString).Trim('/');
        if (!relative.StartsWith(InjectionDefinition.MarkerPrefix, StringComparison.OrdinalIgnoreCase) || relative.Contains('/'))
        {
            return false;
        }

        marker = relative.ToUpperInvariant();
        return true;
    }
}
