using System.Globalization;

namespace Shiny.Infrastructure;

/// <summary>
/// Sets tab badges through MAUI's <see cref="BaseShellItem.BadgeText"/> on the tab's <see cref="ShellSection"/>,
/// so every platform MAUI draws badges on gets them. Colors are left alone - style them with
/// <see cref="BaseShellItem.BadgeColor"/> / <see cref="BaseShellItem.BadgeTextColor"/> in XAML.
/// </summary>
public sealed class ShellTabBadgeManager
{
    readonly Dictionary<string, string> badgeValues = new(StringComparer.Ordinal);
    readonly Lock sync = new();


    /// <summary>
    /// The badges currently set, keyed by tab route (the last segment of the route passed to
    /// <see cref="Set(string, string)"/>). An empty string is a dot badge. A snapshot - safe to read from any thread.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values
    {
        get
        {
            lock (this.sync)
                return new Dictionary<string, string>(this.badgeValues, StringComparer.Ordinal);
        }
    }


    /// <summary>
    /// The badge text on a tab - an empty string for a dot badge - or null when it has none. Accepts the same
    /// routes as <see cref="Set(string, string)"/> - a route name or a full route, of which the last segment is the tab.
    /// </summary>
    public string? Get(string route)
    {
        var normalizedRoute = NormalizeRoute(route);
        lock (this.sync)
            return this.badgeValues.TryGetValue(normalizedRoute, out var value) ? value : null;
    }


    public Task Set(string route, int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Tab badge value must be 0 or greater");

        return this.Set(route, value.ToString(CultureInfo.InvariantCulture));
    }


    /// <summary>
    /// Sets the badge text on a tab. An empty string shows a dot badge. Windows shows non-numeric text as a dot.
    /// </summary>
    public Task Set(string route, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var normalizedRoute = NormalizeRoute(route);
            var section = ResolveSection(normalizedRoute);

            section.BadgeText = text;
            lock (this.sync)
                this.badgeValues[normalizedRoute] = text;
        });
    }


    public Task Clear(string route) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        var normalizedRoute = NormalizeRoute(route);
        var section = ResolveSection(normalizedRoute);

        section.BadgeText = null;
        lock (this.sync)
            this.badgeValues.Remove(normalizedRoute);
    });


    /// <summary>
    /// Puts the badges back on the current Shell's tabs - a switched-in Shell starts without them.
    /// </summary>
    public void ReapplyAll()
    {
        var pending = this.Values
            .Select(badge => (Section: FindSection(badge.Key), Text: badge.Value))
            .Where(x => x.Section != null && x.Section.BadgeText != x.Text)
            .ToList();

        foreach (var badge in pending)
            badge.Section!.BadgeText = badge.Text;
    }


    static string NormalizeRoute(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        var segments = route
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
            throw new ArgumentException("Route must contain at least one segment", nameof(route));

        return segments[^1];
    }


    static ShellSection ResolveSection(string route)
        => FindSection(route) ?? throw new InvalidOperationException($"Could not resolve tab route '{route}' in the active Shell");


    // A tab is a ShellSection - matched by its own route or the route of one of its ShellContents. Every ShellItem
    // is searched, so a tab bar that is not on screen can be badged too.
    static ShellSection? FindSection(string route) => Shell.Current?.Items
        .SelectMany(item => item.Items)
        .FirstOrDefault(section => RouteMatches(section, route) || section.Items.Any(content => RouteMatches(content, route)));


    static bool RouteMatches(BindableObject element, string route)
        => String.Equals(Routing.GetRoute(element), route, StringComparison.Ordinal);
}
