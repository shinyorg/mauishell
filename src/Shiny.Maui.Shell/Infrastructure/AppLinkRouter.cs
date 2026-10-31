using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


/// <summary>
/// Turns an inbound URI into navigation. Matching is delegated to <see cref="AppLinkRegistry"/>;
/// this type owns the cold-start queue and the push-vs-reset decision.
/// </summary>
public class AppLinkRouter(
    ILogger<AppLinkRouter> logger,
    AppLinkRegistry registry,
    AppLinkOptions options,
    ShinyShellNavigator navigator,
    IServiceProvider services
) : IAppLinks, IMauiInitializeService, IDisposable
{
    Application? application;

    /// <summary>
    /// A link can arrive before Shell exists - Android delivers the launch intent during OnCreate,
    /// iOS delivers ContinueUserActivity before the first page appears. Only one is held: a second
    /// link arriving before the flush replaces the first, because nobody deep-links twice inside a
    /// few hundred milliseconds and queueing them would build a nonsense stack.
    /// </summary>
    Uri? pending;
    bool flushed;
    readonly ActivationDeduplicator dedupe = new();


    public void Initialize(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<IApplication>() is not Application app)
            return;

        this.application = app;
        app.PageAppearing += this.OnPageAppearing;
    }


    public void Dispose()
    {
        if (this.application != null)
            this.application.PageAppearing -= this.OnPageAppearing;
    }


    void OnPageAppearing(object? sender, Page page)
    {
        if (this.flushed)
            return;

        this.flushed = true;
        if (this.application != null)
            this.application.PageAppearing -= this.OnPageAppearing;

        var uri = this.pending;
        this.pending = null;
        if (uri == null)
            return;

        logger.LogDebug("[AppLink] Flushing cold-start link '{uri}'", uri);

        // Fire and forget: PageAppearing is a void event and the Shell is mid-layout. Failures
        // are logged inside Navigate.
        _ = this.Navigate(uri, coldStart: true);
    }


    public bool TryResolve(Uri uri, out AppLinkMatch match)
    {
        foreach (var candidate in registry.GetMatches(uri))
        {
            match = candidate;
            return true;
        }

        match = null!;
        return false;
    }


    public Task<AppLinkResult> Handle(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (this.dedupe.IsDuplicate(uri.ToString()))
        {
            // Reported as handled - it was, a moment ago.
            logger.LogDebug("[AppLink] Ignoring duplicate activation '{uri}'", uri);
            return Task.FromResult(AppLinkResult.Navigated);
        }

        if (!this.flushed && Shell.Current == null)
        {
            // Cold start - hold it until the first page appears. Reported as handled because it
            // will be acted on; telling the platform otherwise invites a duplicate delivery.
            logger.LogDebug("[AppLink] Shell not ready, queueing '{uri}'", uri);
            this.pending = uri;
            return Task.FromResult(AppLinkResult.Navigated);
        }

        return this.Navigate(uri, coldStart: false);
    }


    async Task<AppLinkResult> Navigate(Uri uri, bool coldStart)
    {
        try
        {
            foreach (var match in registry.GetMatches(uri))
            {
                var link = registry.Find(match.Template);
                if (link == null)
                    continue;

                var vm = services.GetService(link.ViewModelType);
                if (vm == null)
                {
                    logger.LogWarning("[AppLink] '{vm}' is not registered - skipping template '{template}'", link.ViewModelType, link.Template);
                    continue;
                }

                // A failed bind is a routing miss, not a crash: the next-best template gets a
                // turn before the link is declared unhandled.
                if (!link.Apply(vm, match.Values))
                {
                    logger.LogDebug("[AppLink] Template '{template}' matched '{uri}' but failed to bind", link.Template, uri);
                    continue;
                }

                var route = AppLinkRoutes.Build(match, link, coldStart, options);
                logger.LogInformation("[AppLink] '{uri}' -> '{route}'", uri, route);

                var navigated = await navigator
                    .NavigateToAppLink(link.ViewModelType, vm, route, NavigationSource.AppLink)
                    .ConfigureAwait(false);

                if (navigated)
                    return AppLinkResult.Navigated;

                // A guard turned the link away. That is still the app handling it - reporting it
                // unhandled would invite the platform to open the URL in a browser instead, which
                // is the opposite of what a guard that just blocked it wants.
                logger.LogInformation("[AppLink] '{uri}' was blocked by an interceptor", uri);
                return AppLinkResult.Blocked;
            }

            logger.LogWarning("[AppLink] No route matched '{uri}'", uri);
            if (options.OnUnhandled != null)
            {
                return await options.OnUnhandled.Invoke(uri).ConfigureAwait(false)
                    ? AppLinkResult.Navigated
                    : AppLinkResult.Unhandled;
            }

            return AppLinkResult.Unhandled;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AppLink] Failed to handle '{uri}'", uri);
            return AppLinkResult.Unhandled;
        }
    }
}
