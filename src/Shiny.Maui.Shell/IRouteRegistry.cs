namespace Shiny;


/// <summary>
/// One route mapped with Shiny - by <c>[ShellMap&lt;TPage&gt;]</c> + <c>AddGeneratedMaps()</c>, or by
/// <see cref="ShinyAppBuilder.Add{TPage, TViewModel}"/>.
/// </summary>
/// <param name="Route">The route name, as passed to <see cref="INavigator.NavigateTo(string, bool, bool, CancellationToken, IEnumerable{ValueTuple{string, object}})"/>.</param>
/// <param name="PageType">The page.</param>
/// <param name="ViewModelType">The ViewModel bound to the page.</param>
/// <param name="IsRegisteredWithShell">
/// True when the route is registered with Shell and pushed; false when it is a <c>ShellContent</c>
/// declared in AppShell XAML (<c>registerRoute: false</c>), reached with an absolute <c>//</c> route.
/// </param>
/// <param name="Description">The <c>[ShellMap]</c> description, or null when there is none or the route was mapped by hand.</param>
/// <param name="Parameters">
/// The ViewModel's <c>[ShellProperty]</c> parameters. Null when the route was not described by the
/// source generator (mapped by hand), which is different from an empty list (no parameters).
/// </param>
public sealed record RouteRegistration(
    string Route,
    Type PageType,
    Type ViewModelType,
    bool IsRegisteredWithShell,
    string? Description,
    IReadOnlyList<GeneratedRouteParameter>? Parameters
);


/// <summary>
/// The routes the app has mapped. Lets code that does not configure the app - a library, a test, a
/// diagnostics tool - list where the app can navigate, with page and ViewModel types and, for
/// source-generated maps, descriptions and parameters.
/// </summary>
/// <remarks>
/// Registered by <c>UseShinyShell</c>. Routes declared only in AppShell XAML with no Shiny mapping
/// are Shell's, not Shiny's, and are not listed.
/// </remarks>
public interface IRouteRegistry
{
    /// <summary>Every mapped route, in the order it was mapped.</summary>
    IReadOnlyList<RouteRegistration> Routes { get; }

    /// <summary>The registration for a route name (not a URI - no <c>//</c> or segments), or null.</summary>
    RouteRegistration? GetRoute(string route);

    /// <summary>The registration mapped to a ViewModel type, or null.</summary>
    RouteRegistration? GetRouteForViewModel(Type viewModelType);
}
