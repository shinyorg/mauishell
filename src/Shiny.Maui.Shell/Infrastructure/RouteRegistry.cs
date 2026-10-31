namespace Shiny.Infrastructure;


/// <summary>
/// <see cref="IRouteRegistry"/> over the route map <see cref="ShinyAppBuilder"/> built. The map is
/// complete once <c>UseShinyShell</c> returns, so it is read once, on first use.
/// </summary>
public class RouteRegistry(ShinyAppBuilder builder) : IRouteRegistry
{
    IReadOnlyList<RouteRegistration>? routes;

    public IReadOnlyList<RouteRegistration> Routes => this.routes ??= builder.GetRouteRegistrations();


    public RouteRegistration? GetRoute(string route)
    {
        ArgumentNullException.ThrowIfNull(route);

        foreach (var registration in this.Routes)
        {
            if (registration.Route == route)
                return registration;
        }
        return null;
    }


    public RouteRegistration? GetRouteForViewModel(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        foreach (var registration in this.Routes)
        {
            if (registration.ViewModelType == viewModelType)
                return registration;
        }
        return null;
    }
}
