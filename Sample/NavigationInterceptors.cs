using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;


/// <summary>
/// Flipped from the Push &amp; Pop page so the interceptor can be armed without restarting the app.
/// </summary>
public class NavigationGuardSwitch
{
    /// <summary>
    /// When on, <see cref="AskFirstNavigationInterceptor"/> asks - with an action sheet - what to
    /// do with every navigation to the detail page.
    /// </summary>
    public bool AskBeforeDetail { get; set; }
}


/// <summary>
/// The cross-cutting case: every navigation in the app, whatever started it, through one method.
/// </summary>
public class LoggingNavigationInterceptor(
    ILogger<LoggingNavigationInterceptor> logger,
    INavigationContextAccessor context
) : INavigationInterceptor
{
    // Runs after the guard below, so the log line reflects what the guard let through.
    public int Order => 100;

    public Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "[Interceptor] {From} -> {To} ({Direction}/{Type}) destination VM: {VM}",
            context.Current?.FromUri,
            uri,
            context.Current?.Direction,
            context.Current?.NavigationType,
            viewModel?.GetType().Name ?? "(none)"
        );
        return Task.FromResult(NavigationInterceptorResult.Continue);
    }
}


/// <summary>
/// The guard case, made visible: an action sheet decides - live, per navigation - whether the route
/// goes through, goes somewhere else, or does not go at all. A real guard asks an auth service or a
/// dirty-state flag instead of the user, but the shape of the method is identical: await something,
/// then return Continue, Redirect or Cancel.
/// </summary>
/// <remarks>
/// The interceptor is async all the way, so a dialog is a legal thing to await here - the
/// navigation has not been handed to Shell yet and simply waits on the answer.
/// </remarks>
public class AskFirstNavigationInterceptor(
    NavigationGuardSwitch guards,
    IDialogs dialogs,
    INavigationContextAccessor context
) : INavigationInterceptor
{
    const string LetItGo = "Let it through";
    const string SendElsewhere = "Redirect to Lifecycle";
    const string StopIt = "Stop navigation";

    // Guards run before anything that only observes.
    public int Order => -100;

    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        // Narrow to one destination - without this, every tab tap and back press would prompt.
        // `viewModel` is the real destination instance, already populated with whatever the caller
        // passed, so a guard can decide on the destination's own state and not just its URI.
        if (!guards.AskBeforeDetail || viewModel is not DetailViewModel detail)
            return NavigationInterceptorResult.Continue;

        // Everything the sheet shows comes from the navigation in flight: where the user is now
        // (INavigationContextAccessor), where they asked to go, and the state of the destination
        // ViewModel itself - `Text` is already set when the caller used the configure overload.
        var choice = await dialogs.ActionSheet(
            $"{context.Current?.FromUri} -> {uri} (Text: '{detail.Text}')",
            // Dismissing the sheet lands here too, which is the safe default for a guard.
            cancel: StopIt,
            destruction: null,
            buttons: [LetItGo, SendElsewhere]
        );

        return choice switch
        {
            // The page is built and bound to `detail` exactly as if no interceptor existed.
            LetItGo => NavigationInterceptorResult.Continue,

            // Refactor-safe redirect: the route comes from the ViewModel map, not a string. The
            // detail ViewModel above is dropped - its page is never built - and the whole chain
            // re-runs against the new URI (which is not the detail page, so it is not re-prompted).
            SendElsewhere => NavigationInterceptorResult.Redirect<LifecycleDemoViewModel>(relativeNavigation: true),

            // Nothing moves: the user stays on `context.Current?.FromUri` and no further
            // interceptor in the chain runs.
            _ => NavigationInterceptorResult.Cancel()
        };
    }
}
