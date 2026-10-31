using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


/// <summary>
/// Navigates when a home screen quick action is activated. MAUI owns the platform delivery
/// (<c>AppActions</c>); this only maps the activated id back to a route.
/// </summary>
public class AppShortcutRouter(
    ILogger<AppShortcutRouter> logger,
    AppShortcutRegistry registry,
    AppLinkOptions options,
    IAppShortcutText text,
    ShinyShellNavigator navigator,
    IServiceProvider services
) : IAppShortcuts
{
    bool started;

    /// <summary>
    /// Re-resolves every shortcut's text and pushes the set to the platform again.
    /// </summary>
    public Task Refresh()
    {
        if (!AppActions.Current.IsSupported)
        {
            logger.LogDebug("[Shortcut] Refresh skipped - app actions are not supported here");
            return Task.CompletedTask;
        }

        var actions = registry
            .Shortcuts
            .Select(x => new AppAction(
                x.Id,
                text.GetTitle(x.Route, x.Title),
                text.GetSubtitle(x.Route, x.Subtitle),
                x.Icon
            ))
            .ToList();

        logger.LogDebug("[Shortcut] Refreshing {count} shortcut(s)", actions.Count);
        return AppActions.Current.SetAsync(actions);
    }


    /// <summary>
    /// Entry point for MAUI's OnAppAction callback. Never throws - a quick action that cannot be
    /// resolved should log and do nothing, not take the app down during activation.
    /// </summary>
    /// <returns>False when the shortcut could not be resolved, or when an interceptor blocked it.</returns>
    public async Task<bool> Handle(string id)
    {
        try
        {
            var shortcut = registry.Find(id);
            if (shortcut == null)
            {
                logger.LogWarning("[Shortcut] '{id}' is not registered", id);
                return false;
            }

            var vm = services.GetService(shortcut.ViewModelType);
            if (vm == null)
            {
                logger.LogWarning("[Shortcut] '{vm}' is not registered in DI", shortcut.ViewModelType);
                return false;
            }

            // Declared shortcuts carry no values (SHINY010 guarantees the route needs none); a
            // hand-registered one supplies them through the configure callback.
            shortcut.Configure?.Invoke(vm);

            var coldStart = !this.started;
            this.started = true;

            var route = AppLinkRoutes.Build(shortcut.Route, shortcut.RegisterRoute, coldStart, options.DefaultRoot);
            logger.LogInformation("[Shortcut] '{id}' -> '{route}'", id, route);

            var navigated = await navigator
                .NavigateToAppLink(shortcut.ViewModelType, vm, route, NavigationSource.Shortcut)
                .ConfigureAwait(false);

            if (!navigated)
                logger.LogInformation("[Shortcut] '{id}' was blocked by an interceptor", id);

            return navigated;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Shortcut] Failed to handle '{id}'", id);
            return false;
        }
    }
}
