namespace Shiny.Infrastructure;


/// <summary>
/// The small amount of Shell URI grammar the interceptor pipeline needs. Pure string work, kept
/// separate so the redirect rules can be tested without a Shell.
/// </summary>
public static class NavigationUri
{
    /// <summary>
    /// Cleans up a URI written by hand in an interceptor. A single leading slash is promoted to
    /// the Shell absolute form, because <c>"/login"</c> means the same thing to everyone who
    /// writes it and nothing at all to Shell.
    /// </summary>
    public static string Normalize(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var trimmed = uri.Trim();
        if (trimmed.Length == 0)
            return trimmed;

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
            return trimmed;

        if (trimmed[0] == '/')
            return "/" + trimmed;

        return trimmed;
    }


    /// <summary>
    /// Classifies a URI for <see cref="NavigationEventArgs"/> - the same inference Shell itself
    /// makes from the prefix.
    /// </summary>
    public static NavigationType GetNavigationType(string uri)
    {
        if (String.IsNullOrWhiteSpace(uri))
            return NavigationType.Push;

        if (uri.StartsWith("//", StringComparison.Ordinal))
            return NavigationType.SetRoot;

        if (uri.StartsWith("..", StringComparison.Ordinal))
            return NavigationType.GoBack;

        return NavigationType.Push;
    }


    /// <summary>
    /// True when the URI pushes more than one page in a single navigation (<c>A/B/C</c>,
    /// <c>../A/B</c>). Shell pushes the last page and inserts the others before it. An absolute
    /// URI's first segment selects the root rather than pushing, so it is not counted.
    /// </summary>
    public static bool PushesSeveralPages(string uri)
    {
        if (String.IsNullOrWhiteSpace(uri))
            return false;

        var value = uri;
        var query = value.IndexOfAny(['?', '#']);
        if (query >= 0)
            value = value.Substring(0, query);

        var pushed = value
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(x => x != "..");

        if (value.StartsWith("//", StringComparison.Ordinal))
            pushed--;

        return pushed > 1;
    }


    /// <summary>
    /// The route the navigation lands on - the last real segment, ignoring the query string and
    /// any leading pops. Returns null for a pure back navigation, which lands on a page that
    /// already exists.
    /// </summary>
    public static string? GetTargetRoute(string uri)
    {
        if (String.IsNullOrWhiteSpace(uri))
            return null;

        var value = uri;

        var query = value.IndexOfAny(['?', '#']);
        if (query >= 0)
            value = value.Substring(0, query);

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var segment = segments[i].Trim();
            if (segment.Length == 0 || segment == "..")
                continue;

            return segment;
        }
        return null;
    }
}
