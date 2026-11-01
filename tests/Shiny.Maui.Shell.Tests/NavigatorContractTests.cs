using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// Source-level regression tests for the post-GoToAsync direct-BindingContext-read
/// refactor in <c>ShellNavigator.NavigateTo&lt;TViewModel&gt;</c> and
/// <c>NavigationBuilder.Navigate</c>.
///
/// Background
/// ----------
/// Both methods previously subscribed to a static <c>ShinyRouteFactory.PageResolved</c>
/// event to detect when the destination page had been resolved by DI. That event was
/// only raised by <c>ShinyRouteFactory.GetOrCreate</c>, which is only invoked when MAUI
/// Shell instantiates a page through a registered <c>RouteFactory</c>. Pages declared
/// in an AppShell.xaml as <c>&lt;ShellContent ContentTemplate="{DataTemplate ...}"&gt;</c>
/// (paired with <c>[ShellMap&lt;TPage&gt;(registerRoute: false)]</c>) are constructed via
/// the DataTemplate, never go through the factory, and therefore never raised
/// <c>PageResolved</c>.
///
/// The bug
/// -------
/// <c>NavigateTo&lt;TVM&gt;(relativeNavigation: false)</c> targeting a ShellContent route
/// would await a <c>TaskCompletionSource</c> that never completed, leaking the
/// subscribed handler. A later unrelated navigation through a registered route would
/// raise <c>PageResolved</c> with a different page, wake the leaked handler, and throw
/// <c>InvalidOperationException("Page BindingContext is not of type '&lt;original VM&gt;'")</c>.
/// On iOS the exception surfaced asynchronously through
/// <c>NSAsyncSynchronizationContextDispatcher</c> against a stale call site, making the
/// crash look like it originated from a later, unrelated navigation.
///
/// The fix
/// -------
/// <c>NavigateTo&lt;TViewModel&gt;</c> reads <c>Shell.Current.CurrentPage.BindingContext</c>
/// directly after <c>Shell.GoToAsync</c> returns. <c>NavigationBuilder.Navigate</c>
/// walks <c>Shell.Current.Navigation.NavigationStack</c> and applies each segment's
/// configure callback by index. The static <c>PageResolved</c> event is gone.
///
/// Runtime coverage gap
/// --------------------
/// A true end-to-end test of ShellContent-declared route navigation requires a MAUI
/// test host (<c>Shell.Current</c>, <c>IPlatformApplication</c>, handlers). The current
/// test project only references the source generator and runs pure-.NET unit tests, so
/// the lock-in here is at the source-contract level. If a MAUI device-test project is
/// added in the future, port these regressions into a runtime navigation scenario.
/// </summary>
public class NavigatorContractTests
{
    static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Directory.Build.props")))
            dir = Path.GetDirectoryName(dir);

        if (dir == null)
            throw new InvalidOperationException(
                $"Could not locate repo root (no Directory.Build.props found above '{AppContext.BaseDirectory}')."
            );

        return dir;
    }

    static string ReadInfrastructureSource(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "Shiny.Maui.Shell", "Infrastructure", fileName);
        File.Exists(path).ShouldBeTrue($"Expected source file at '{path}'.");
        return File.ReadAllText(path);
    }

    [Fact]
    public void ShinyRouteFactory_DoesNotDeclarePageResolvedEvent()
    {
        var source = ReadInfrastructureSource("ShinyRouteFactory.cs");

        source.ShouldNotContain("PageResolved",
            customMessage:
            "ShinyRouteFactory.PageResolved was removed because it never fired for ShellContent-declared " +
            "pages and leaked subscribers across navigations. Reintroducing it reopens the " +
            "'Page BindingContext is not of type X' crash for any caller doing " +
            "NavigateTo<TVM>(relativeNavigation: false) into a XAML-declared ShellContent route."
        );
    }

    [Fact]
    public void ShellNavigator_DoesNotSubscribeToPageResolved()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        source.ShouldNotContain("ShinyRouteFactory.PageResolved +=",
            customMessage:
            "NavigateTo<TViewModel> must not subscribe to a static page-resolved event. " +
            "That subscription leaked when the target was a ShellContent route (the event never fired) " +
            "and the leaked handler later threw against an unrelated page from a different navigation."
        );
        source.ShouldNotContain("ShinyRouteFactory.PageResolved -=",
            customMessage:
            "The PageResolved unsubscribe was removed alongside the subscribe in the refactor."
        );
    }

    [Fact]
    public void ShellNavigator_PinsTheConfiguredViewModelInsteadOfProbingTheCurrentPage()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        source.ShouldContain("Pins = [new PinnedViewModel(typeof(TViewModel), vm!)]",
            customMessage:
            "NavigateTo<TViewModel> must resolve and configure the viewmodel up front and pin it for the " +
            "apply sites. Reading Shell.Current.CurrentPage.BindingContext after GoToAsync returns - the " +
            "first replacement for the broken static-event flow - races Shell's own scheduling on Android, " +
            "where the awaiter can resolve before OnNavigated / PageAppearing have fired."
        );
    }


    [Fact]
    public void NavigationBuilder_DoesNotSubscribeToPageResolved()
    {
        var source = ReadInfrastructureSource("NavigationBuilder.cs");

        source.ShouldNotContain("ShinyRouteFactory.PageResolved +=",
            customMessage:
            "NavigationBuilder.Navigate must not subscribe to a static page-resolved event. " +
            "The static event is shared across all in-flight navigations and causes handler crosstalk " +
            "whenever two builders or a builder + NavigateTo<TVM> overlap."
        );
        source.ShouldNotContain("ShinyRouteFactory.PageResolved -=",
            customMessage:
            "The PageResolved unsubscribe was removed alongside the subscribe in the refactor."
        );
    }

    [Fact]
    public void NavigationBuilder_PinsResolvedViewModels()
    {
        var source = ReadInfrastructureSource("NavigationBuilder.cs");

        source.ShouldContain("PinnedViewModel",
            customMessage:
            "NavigationBuilder.Navigate must pre-resolve each typed segment's viewmodel, apply its configure " +
            "callback synchronously, and hand the instances to the navigator as pins - which pins them on " +
            "ShellNavigationConfigurator via EnqueueResolved. The apply sites (ShinyRouteFactory.GetOrCreate, " +
            "ShinyShell.OnNavigated, AppOnPageAppearing) consume the pinned instances when Shell realises each " +
            "segment's page. This replaces the v6.1 stack-walk approach which raced against Shell's " +
            "PageAppearing scheduling on Android."
        );
        source.ShouldContain("GetRequiredService(seg.ViewModelType)",
            customMessage: "Each typed segment's viewmodel must still be resolved and configured before navigation starts."
        );

        // Both ways of asking to skip the guards have to reach the request, or the fluent form
        // would silently do nothing.
        source.ShouldContain("bypassInterceptors || this.skipInterceptors",
            customMessage: "NavigationBuilder must honour both BypassInterceptors() and Navigate(bypassInterceptors: true)."
        );

        // The pinning itself moved into the navigator when interception was added, so every
        // navigation path shares one place that pins, cancels and redirects.
        var navigator = ReadInfrastructureSource("ShellNavigator.cs");
        navigator.ShouldContain("configurator.EnqueueResolved",
            customMessage: "ShinyShellNavigator.ExecuteNavigation must pin every pre-resolved viewmodel before Shell builds a page."
        );
    }


    [Fact]
    public void Navigator_RollsBackPinnedEntriesOnlyOnFailure()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        var execute = source.Substring(source.IndexOf("async Task ExecuteNavigation(", StringComparison.Ordinal));
        execute = execute.Substring(0, execute.IndexOf("\n    }\n", StringComparison.Ordinal));

        execute.ShouldNotContain("finally",
            customMessage:
            "ExecuteNavigation must not unconditionally dispose pinned subscriptions in a finally block - " +
            "on Android the apply sites fire after the navigation task completes, so disposing on success " +
            "would cause fallback DI resolves and lose configured viewmodels. Dispose only inside a catch block."
        );
        execute.ShouldContain("catch",
            customMessage: "A failed navigation must roll its pins back, or they leak onto the next navigation to the same viewmodel type."
        );
    }


    [Fact]
    public void ShellNavigating_AsksNavigationConfirmationBeforeTheInterceptors()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        var handler = source.Substring(source.IndexOf("async void OnShellNavigating(", StringComparison.Ordinal));

        var confirm = handler.IndexOf("confirm.CanNavigate()", StringComparison.Ordinal);
        var intercept = handler.IndexOf(".Run(", StringComparison.Ordinal);

        confirm.ShouldBeGreaterThan(-1,
            customMessage:
            "OnShellNavigating must still ask INavigationConfirmation.CanNavigate on the page being left. " +
            "It is the ViewModel-level guard and predates interceptors - an app-wide pipeline must not " +
            "quietly replace it."
        );
        confirm.ShouldBeLessThan(intercept,
            customMessage:
            "CanNavigate must be asked before the interceptors run: it answers 'may I leave this page', " +
            "which is settled before anything about the destination matters."
        );
    }

    [Fact]
    public void NavigationConfirmation_StaysScopedToUserDrivenNavigation()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        // Shell raises Navigating for programmatic navigation too; the flag is what keeps
        // CanNavigate limited to tab taps, flyout items and the hardware back button. Removing it
        // would silently start prompting on every NavigateTo the app makes.
        source.ShouldContain("if (this.isProgrammaticNavigation)",
            customMessage: "The programmatic-navigation short-circuit in OnShellNavigating is what scopes INavigationConfirmation to user-driven navigation."
        );

        var core = source.Substring(source.IndexOf("async Task<bool> RunNavigationCore(", StringComparison.Ordinal));
        core = core.Substring(0, core.IndexOf("\n    }\n", StringComparison.Ordinal));
        core.ShouldNotContain("INavigationConfirmation",
            customMessage:
            "Programmatic navigation deliberately does not ask INavigationConfirmation - that is the " +
            "documented v6 behaviour. App-wide rules belong in an INavigationInterceptor."
        );
    }

    [Fact]
    public void Navigator_RunsInterceptorsOnEveryNavigationPath()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        // Every navigation entry point must build a NavigationRequest and go through
        // RunNavigation/RunNavigationCore - a path that calls Shell.GoToAsync on its own would
        // silently skip the guards, which is the failure mode this whole feature exists to avoid.
        source.ShouldContain("interceptors\n            .Run(",
            customMessage: "RunNavigationCore must run the interceptor pipeline before navigating."
        );

        var gotoCalls = System.Text.RegularExpressions.Regex.Matches(source, @"GoToAsync\(").Count;
        gotoCalls.ShouldBe(1,
            customMessage:
            "Shell.GoToAsync must be called from exactly one place (ExecuteNavigation). Any other call site " +
            "bypasses INavigationInterceptor."
        );
    }


    [Fact]
    public void ShellNavigating_ObservingNeverTakesADeferral()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");
        var handler = source.Substring(source.IndexOf("async void OnShellNavigating(", StringComparison.Ordinal));
        handler = handler.Substring(0, handler.IndexOf("\n    }\n", StringComparison.Ordinal));

        var start = handler.IndexOf("observers.StartShell(", StringComparison.Ordinal);
        var bail = handler.IndexOf("if (!shellArgs.CanCancel || (vm is not INavigationConfirmation && !hasInterceptors))", StringComparison.Ordinal);
        var deferral = handler.IndexOf("GetDeferral()", StringComparison.Ordinal);

        start.ShouldBeGreaterThan(-1,
            customMessage: "Shell-driven navigation (tabs, flyout, back) must be reported to INavigationObservers."
        );
        bail.ShouldBeGreaterThan(-1,
            customMessage:
            "The early return for an app with no INavigationConfirmation and no interceptors must stay - " +
            "observers must not make Shiny take a Shell deferral."
        );
        start.ShouldBeLessThan(bail,
            customMessage: "The navigation is reported before the early return, so observers see it even when nothing intercepts."
        );
        bail.ShouldBeLessThan(deferral,
            customMessage: "No deferral may be taken before the early return - observing must not change how Shell navigates."
        );
    }


    [Fact]
    public void ShellNavigated_FinishesShellDrivenNavigation()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        source.ShouldContain("shell.Navigated += this.OnShellNavigated",
            customMessage:
            "A Shell-driven navigation Shiny lets through is only known to have completed when Shell raises " +
            "Navigated - without the subscription, observers would never hear that a tab tap landed."
        );
        source.ShouldContain("observers.ShellNavigated(");
    }


    [Fact]
    public void Navigator_ReportsEveryProgrammaticOutcome()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");
        var core = source.Substring(source.IndexOf("async Task<bool> RunNavigationCore(NavigationRequest request, NavigationStarted? observed)", StringComparison.Ordinal));
        core = core.Substring(0, core.IndexOf("\n    }\n", StringComparison.Ordinal));

        core.ShouldContain("NavigationOutcome.Cancelled");
        core.ShouldContain("NavigationOutcome.Redirected");
        core.ShouldContain("NavigationOutcome.Completed");
        core.ShouldContain("interception.CancelledBy",
            customMessage: "Observers are told which interceptor cancelled."
        );

        // Failure is reported by the wrapper, so every throw - interceptor, Shell, token - is covered.
        source.ShouldContain("observers.Finish(observed, NavigationOutcome.Failed, request.Uri, exception: ex);");
    }


    [Fact]
    public void AppLinksAndShortcuts_IdentifyThemselvesToObservers()
    {
        ReadInfrastructureSource("AppLinkRouter.cs").ShouldContain("NavigationSource.AppLink");
        ReadInfrastructureSource("AppShortcutRouter.cs").ShouldContain("NavigationSource.Shortcut");
    }
}
