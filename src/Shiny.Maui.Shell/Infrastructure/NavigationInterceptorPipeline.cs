using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


/// <summary>
/// What the interceptor chain decided about one navigation.
/// </summary>
/// <param name="IsCancelled">True when an interceptor stopped the navigation - nothing else applies.</param>
/// <param name="Uri">The URI to navigate to, which differs from the requested one after a redirect.</param>
/// <param name="ViewModel">The destination ViewModel, or null when the route has no mapping.</param>
/// <param name="ViewModelType">The mapped ViewModel type for <paramref name="Uri"/>.</param>
/// <param name="IsRedirected">True when at least one interceptor redirected.</param>
/// <param name="IsViewModelResolved">
/// True when the pipeline created <paramref name="ViewModel"/> itself, which is what tells the
/// navigator to pin it for the page about to be built. False when the instance came from the
/// caller (it decides) or from a page that already exists (nothing to pin).
/// </param>
public sealed record NavigationInterception(
    bool IsCancelled,
    string Uri,
    object? ViewModel,
    Type? ViewModelType,
    bool IsRedirected,
    bool IsViewModelResolved
)
{
    /// <summary>The interceptor that cancelled, when <see cref="IsCancelled"/> is true.</summary>
    public INavigationInterceptor? CancelledBy { get; init; }

    /// <summary>Every redirect the chain made, in order. Empty when <see cref="IsRedirected"/> is false.</summary>
    public IReadOnlyList<NavigationRedirect> Redirects { get; init; } = Array.Empty<NavigationRedirect>();
}


/// <summary>
/// Runs the registered <see cref="INavigationInterceptor"/>s over a navigation and reports the
/// outcome. Every navigation path in the library funnels through here, so a guard written once
/// covers <see cref="INavigator"/> calls, app links, shortcuts and user-driven Shell navigation
/// alike.
/// </summary>
public class NavigationInterceptorPipeline(
    ILogger<NavigationInterceptorPipeline> logger,
    IServiceProvider services,
    ShinyAppBuilder appBuilder,
    NavigationContextAccessor contextAccessor
)
{
    /// <summary>
    /// How many times a navigation may be redirected before it is called a loop. A guard pair that
    /// bounces the user back and forth would otherwise hang the navigation forever.
    /// </summary>
    public const int MaxRedirects = 10;

    bool? any;

    /// <summary>
    /// Whether anything is registered at all. Checked before the expensive parts of a navigation
    /// (taking a Shell deferral, resolving a destination ViewModel) so apps without interceptors
    /// pay nothing.
    /// </summary>
    public bool HasInterceptors => this.any ??= services.GetServices<INavigationInterceptor>().Any();


    /// <summary>
    /// The registered interceptors in the order they run: <see cref="INavigationInterceptor.Order"/>
    /// first, registration order for ties (LINQ's sort is stable, which is what makes the tie-break
    /// predictable rather than incidental).
    /// </summary>
    IReadOnlyList<INavigationInterceptor> GetOrdered()
        => services
            .GetServices<INavigationInterceptor>()
            .OrderBy(x => x.Order)
            .ToList();


    /// <summary>
    /// Runs the chain, following redirects until it settles.
    /// </summary>
    /// <param name="uri">The requested destination.</param>
    /// <param name="navigationType">How the destination would be reached.</param>
    /// <param name="parameters">Navigation arguments, surfaced through <see cref="INavigationContextAccessor"/>.</param>
    /// <param name="viewModel">The destination ViewModel when the caller already has one (typed navigation, app links).</param>
    /// <param name="viewModelType">Its type.</param>
    /// <param name="resolveViewModel">
    /// True when the pipeline should resolve the destination ViewModel itself because the caller
    /// has none and a page is about to be built for it. False for back navigation and user-driven
    /// Shell navigation, where constructing a ViewModel would be inventing one nobody asked for.
    /// </param>
    /// <param name="cancellationToken">Handed to each interceptor; checked between them.</param>
    public async Task<NavigationInterception> Run(
        string uri,
        NavigationType navigationType,
        IReadOnlyDictionary<string, object> parameters,
        object? viewModel = null,
        Type? viewModelType = null,
        bool resolveViewModel = false,
        CancellationToken cancellationToken = default
    )
    {
        var interceptors = this.GetOrdered();
        if (interceptors.Count == 0)
            return new NavigationInterception(false, uri, viewModel, viewModelType, false, false);

        var currentUri = uri;
        var currentType = navigationType;
        var vm = viewModel;
        var vmType = viewModelType;
        var resolved = false;
        List<NavigationRedirect>? redirectChain = null;

        if (vm == null && resolveViewModel)
            (vm, vmType, resolved) = this.ResolveTarget(currentUri);

        // Read once, before anything runs - an interceptor that awaits a dialog would otherwise
        // see a Shell that has already moved on.
        var fromUri = Shell.Current?.CurrentState?.Location?.ToString();
        var fromViewModel = Shell.Current?.CurrentPage?.BindingContext;

        for (var redirects = 0; redirects <= MaxRedirects; redirects++)
        {
            string? redirectTo = null;

            using (contextAccessor.Push(new NavigationContext(fromUri, fromViewModel, currentUri, currentType, parameters, redirects)))
            {
                foreach (var interceptor in interceptors)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await interceptor
                        .InterceptNavigationAsync(currentUri, vm, cancellationToken)
                        .ConfigureAwait(false);

                    if (result == null)
                        continue;

                    if (result.CancelNavigation)
                    {
                        logger.LogInformation(
                            "[Interceptor] '{interceptor}' cancelled navigation to '{uri}'",
                            interceptor.GetType().Name,
                            currentUri
                        );
                        return new NavigationInterception(true, currentUri, vm, vmType, redirects > 0, resolved)
                        {
                            CancelledBy = interceptor,
                            Redirects = (IReadOnlyList<NavigationRedirect>?)redirectChain ?? Array.Empty<NavigationRedirect>()
                        };
                    }

                    var target = this.GetRedirect(result, interceptor);
                    if (target == null)
                        continue;

                    if (target == currentUri)
                    {
                        // Redirecting to where we are already going is a no-op, not a loop - a
                        // guard that unconditionally points at "//login" says this every time the
                        // user navigates to the login page.
                        logger.LogDebug(
                            "[Interceptor] '{interceptor}' redirected to the current destination '{uri}' - ignored",
                            interceptor.GetType().Name,
                            currentUri
                        );
                        continue;
                    }

                    logger.LogInformation(
                        "[Interceptor] '{interceptor}' redirected '{from}' to '{to}'",
                        interceptor.GetType().Name,
                        currentUri,
                        target
                    );
                    (redirectChain ??= new()).Add(new NavigationRedirect(currentUri, target, interceptor));
                    redirectTo = target;
                    break;
                }
            }

            if (redirectTo == null)
            {
                return new NavigationInterception(false, currentUri, vm, vmType, redirects > 0, resolved)
                {
                    Redirects = (IReadOnlyList<NavigationRedirect>?)redirectChain ?? Array.Empty<NavigationRedirect>()
                };
            }

            // The original destination is abandoned - its ViewModel is dropped rather than bound,
            // and the chain restarts so the new destination is guarded just as thoroughly.
            currentUri = redirectTo;
            currentType = NavigationUri.GetNavigationType(currentUri);
            (vm, vmType, resolved) = this.ResolveTarget(currentUri);
        }

        throw new InvalidOperationException(
            $"Navigation to '{uri}' was redirected more than {MaxRedirects} times and was abandoned. Check your INavigationInterceptor registrations for a redirect loop."
        );
    }


    /// <summary>
    /// Turns a result into the URI to redirect to, or null when it isn't a redirect.
    /// </summary>
    string? GetRedirect(NavigationInterceptorResult result, INavigationInterceptor interceptor)
    {
        if (!String.IsNullOrWhiteSpace(result.RedirectUri))
        {
            if (result.RedirectViewModelType != null)
            {
                logger.LogWarning(
                    "[Interceptor] '{interceptor}' set both RedirectUri and RedirectViewModelType - RedirectUri wins",
                    interceptor.GetType().Name
                );
            }
            return NavigationUri.Normalize(result.RedirectUri!);
        }

        if (result.RedirectViewModelType == null)
            return null;

        var route = appBuilder.GetRouteForViewModel(result.RedirectViewModelType)
            ?? throw new InvalidOperationException(
                $"'{interceptor.GetType().Name}' redirected to '{result.RedirectViewModelType}', which is not mapped to a page. Map it with ShinyAppBuilder.Add<TPage, TViewModel>() or [ShellMap<TPage>]."
            );

        return result.RedirectRelative ? route : "//" + route;
    }


    /// <summary>
    /// Resolves the ViewModel mapped to the route a URI lands on. An unmapped route is not an
    /// error - interceptors are told about the navigation either way, just without a ViewModel.
    /// </summary>
    (object? ViewModel, Type? ViewModelType, bool Resolved) ResolveTarget(string uri)
    {
        var route = NavigationUri.GetTargetRoute(uri);
        if (route == null)
            return (null, null, false);

        var info = appBuilder.GetRouteInfo(route);
        if (info == null)
            return (null, null, false);

        var vm = services.GetService(info.Value.ViewModelType);
        if (vm == null)
        {
            logger.LogWarning(
                "[Interceptor] ViewModel '{vm}' for route '{route}' could not be resolved",
                info.Value.ViewModelType,
                route
            );
            return (null, info.Value.ViewModelType, false);
        }

        return (vm, info.Value.ViewModelType, true);
    }
}
