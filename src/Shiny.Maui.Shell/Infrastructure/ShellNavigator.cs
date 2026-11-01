using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


public class ShinyShellNavigator(
    ILogger<ShinyShellNavigator> logger,
    IMainThread mainThread,
    ShinyAppBuilder navBuilder,
    ShellTabBadgeManager tabBadgeManager,
    ShellNavigationConfigurator configurator,
    NavigationInterceptorPipeline interceptors,
    NavigationObservers observers,
    DialogObservers dialogObservers
) : INavigator, IMauiInitializeService, IDisposable
{
    public event EventHandler<NavigationEventArgs>? Navigating;
    public event EventHandler<NavigatedEventArgs>? Navigated;
    IServiceProvider services = null!;
    Application application = null!;

    internal IServiceProvider Services => this.services;

    record PendingNavigation(string ToUri, NavigationType NavigationType, IReadOnlyDictionary<string, object> Parameters);
    PendingNavigation? pendingNavigation;
    bool isProgrammaticNavigation;

    public void Initialize(IServiceProvider serviceProvider)
    {
        var appService = serviceProvider.GetService<IApplication>();
        if (appService is not Application app)
            throw new InvalidOperationException($"Invalid MAUI Application - {application.GetType()}");

        this.services = serviceProvider;
        this.application = app;
        app.DescendantAdded += this.AppOnDescendantAdded;
        app.DescendantRemoved += this.AppOnDescendantRemoved;
        app.PageAppearing += this.AppOnPageAppearing;
        app.PageDisappearing += this.AppOnPageDisappearing;

        // The initial page may have already appeared before event handlers were registered
        var currentPage = Shell.Current?.CurrentPage;
        if (currentPage != null)
            this.AppOnPageAppearing(this, currentPage);
    }
    
    
    public void Dispose()
    {
        if (this.application == null)
            return;
        
        this.application.DescendantAdded -= this.AppOnDescendantAdded;
        this.application.DescendantRemoved -= this.AppOnDescendantRemoved;
        this.application.PageAppearing -= this.AppOnPageAppearing;
        this.application.PageDisappearing -= this.AppOnPageDisappearing;
    }

    
    void RaiseNavigating(Shell shell, string toUri, NavigationType navigationType, IDictionary<string, object> parameters)
    {
        var readOnlyParams = new Dictionary<string, object>(parameters);
        this.pendingNavigation = new PendingNavigation(toUri, navigationType, readOnlyParams);

        try
        {
            this.Navigating?.Invoke(this, new NavigationEventArgs(
                shell.CurrentState?.Location?.ToString(),
                shell.CurrentPage?.BindingContext,
                toUri,
                navigationType,
                readOnlyParams
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigating event handler");
        }
    }


    void RaiseNavigated(object? toViewModel)
    {
        var pending = this.pendingNavigation;
        this.pendingNavigation = null;
        if (pending == null)
            return;

        try
        {
            this.Navigated?.Invoke(this, new NavigatedEventArgs(
                pending.ToUri,
                toViewModel,
                pending.NavigationType,
                pending.Parameters
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigated event handler");
        }
    }


    public INavigationBuilder CreateBuilder(bool fromRoot = false)
        => new NavigationBuilder(this, navBuilder, fromRoot);


    /// <summary>
    /// One navigation, before the interceptors have had their say. Everything that navigates -
    /// route, typed, builder, back, app link, shortcut - describes itself with one of these so a
    /// guard written once applies to all of them.
    /// </summary>
    /// <param name="Uri">The requested destination, exactly as Shell would receive it.</param>
    /// <param name="NavigationType">How the destination is reached.</param>
    /// <param name="Parameters">Navigation arguments.</param>
    internal sealed record NavigationRequest(
        string Uri,
        NavigationType NavigationType,
        Dictionary<string, object> Parameters
    )
    {
        /// <summary>
        /// ViewModels the caller has already resolved and populated, in the order Shell realises
        /// their pages. They are pinned for the apply sites - and dropped if an interceptor
        /// redirects, because their pages are then never built.
        /// </summary>
        public IReadOnlyList<PinnedViewModel> Pins { get; init; } = Array.Empty<PinnedViewModel>();

        /// <summary>The destination ViewModel handed to interceptors. Null asks the pipeline to resolve one when <see cref="ResolveViewModel"/> allows it.</summary>
        public object? ViewModel { get; init; }

        /// <summary>Type of <see cref="ViewModel"/>.</summary>
        public Type? ViewModelType { get; init; }

        /// <summary>
        /// Whether the pipeline may construct the destination ViewModel for the interceptors.
        /// False for back navigation, where the destination already exists.
        /// </summary>
        public bool ResolveViewModel { get; init; }

        /// <summary>Enables the Linux direct-push fallback (see <see cref="ExecuteNavigation"/>).</summary>
        public bool AllowLinuxDirectPush { get; init; }

        /// <summary>Skips the interceptor chain entirely - for the navigation a guard itself issues.</summary>
        public bool BypassInterceptors { get; init; }

        /// <summary>Handed to the interceptors.</summary>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>Who asked - reported to <see cref="INavigationObserver"/>s.</summary>
        public NavigationSource Source { get; init; } = NavigationSource.Navigator;
    }

    internal readonly record struct PinnedViewModel(Type Type, object Instance);


    internal Task<bool> RunNavigation(NavigationRequest request)
        => mainThread.InvokeOnMainThreadAsync(() => this.RunNavigationCore(request));


    /// <summary>
    /// Interception, then navigation. Runs on the main thread so an interceptor can put a dialog
    /// up without dispatching for itself.
    /// </summary>
    async Task<bool> RunNavigationCore(NavigationRequest request)
    {
        var observed = this.StartObserving(request.Source, request.NavigationType, request.Uri, request.Parameters);
        try
        {
            return await this.RunNavigationCore(request, observed).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            observers.Finish(observed, NavigationOutcome.Failed, request.Uri, exception: ex);
            throw;
        }
    }


    async Task<bool> RunNavigationCore(NavigationRequest request, NavigationStarted? observed)
    {
        if (request.BypassInterceptors)
        {
            logger.LogDebug("[Navigation] '{uri}' is bypassing the interceptors", request.Uri);
            await this
                .ExecuteNavigation(request.Uri, request.NavigationType, request.Parameters, request.Pins, request.AllowLinuxDirectPush)
                .ConfigureAwait(true);

            observers.Finish(observed, NavigationOutcome.Completed, request.Uri);
            return true;
        }

        var interception = await interceptors
            .Run(
                request.Uri,
                request.NavigationType,
                request.Parameters,
                request.ViewModel,
                request.ViewModelType,
                request.ResolveViewModel,
                request.CancellationToken
            )
            .ConfigureAwait(true);

        if (interception.IsCancelled)
        {
            // Cancelling is a decision, not a failure - the caller's task completes normally with
            // false, and any ViewModel resolved for the abandoned destination is never pinned.
            logger.LogDebug("[Navigation] '{uri}' was cancelled by an interceptor", request.Uri);
            observers.Finish(
                observed,
                NavigationOutcome.Cancelled,
                interception.Uri,
                interception.Redirects,
                interception.CancelledBy
            );
            return false;
        }

        var uri = interception.Uri;
        var navType = request.NavigationType;
        var pins = request.Pins;

        if (interception.IsRedirected)
        {
            // The requested destination is gone, so its pins go with it - keeping them would leak
            // a configured ViewModel onto whatever navigates to that type next.
            navType = NavigationUri.GetNavigationType(uri);
            pins = this.CanPinResolved(interception, uri)
                ? [new PinnedViewModel(interception.ViewModelType!, interception.ViewModel!)]
                : [];
        }
        else if (this.CanPinResolved(interception, uri))
        {
            // The pipeline built the destination ViewModel so the interceptors could see it - bind
            // that instance rather than letting the route factory resolve a second one, or the
            // mutations an interceptor made would be thrown away. It is appended because the
            // pipeline only ever resolves the destination, which Shell realises last.
            pins = [..pins, new PinnedViewModel(interception.ViewModelType!, interception.ViewModel!)];
        }

        await this.ExecuteNavigation(uri, navType, request.Parameters, pins, request.AllowLinuxDirectPush).ConfigureAwait(true);
        observers.Finish(
            observed,
            interception.IsRedirected ? NavigationOutcome.Redirected : NavigationOutcome.Completed,
            uri,
            interception.Redirects
        );
        return true;
    }


    /// <summary>
    /// Reports a navigation starting to the observers, reading where the user is from Shell. Does
    /// not touch Shell at all when nobody is observing.
    /// </summary>
    NavigationStarted? StartObserving(
        NavigationSource source,
        NavigationType navigationType,
        string uri,
        IReadOnlyDictionary<string, object> parameters
    )
    {
        if (!observers.HasObservers)
            return null;

        var shell = Shell.Current;
        return observers.Start(
            source,
            navigationType,
            uri,
            parameters,
            shell?.CurrentState?.Location?.ToString(),
            shell?.CurrentPage?.BindingContext
        );
    }


    /// <summary>
    /// Whether a ViewModel the pipeline built for the interceptors should also be bound to the
    /// destination page.
    /// </summary>
    /// <remarks>
    /// Only for routes registered with Shell, which are always built through
    /// <see cref="ShinyRouteFactory"/> and therefore always consume their pin. A route declared as
    /// a <c>ShellContent</c> in AppShell XAML may already be realised and bound, in which case the
    /// apply sites leave it alone - and the pin would sit in the queue waiting to surprise a later
    /// navigation to the same ViewModel type. Interceptors still see the instance either way; what
    /// they do not get on an already-realised ShellContent page is the guarantee that a change they
    /// make to it reaches the screen.
    /// </remarks>
    bool CanPinResolved(NavigationInterception interception, string uri)
    {
        if (!interception.IsViewModelResolved || interception.ViewModelType == null || interception.ViewModel == null)
            return false;

        var route = NavigationUri.GetTargetRoute(uri);
        return route != null && navBuilder.GetRouteInfo(route)?.RegisterRoute == true;
    }


    /// <summary>
    /// The navigation itself, with no interception. Must be called on the main thread.
    /// </summary>
    async Task ExecuteNavigation(
        string uri,
        NavigationType navType,
        Dictionary<string, object> parameters,
        IReadOnlyList<PinnedViewModel> pins,
        bool allowLinuxDirectPush
    )
    {
        var shell = Shell.Current;
        if (shell.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        this.RaiseNavigating(shell, uri, navType, parameters);
        this.isProgrammaticNavigation = true;

        // Pin every pre-resolved ViewModel before Shell builds a page. Whichever apply site fires
        // (ShinyRouteFactory.GetOrCreate for registered routes, ShinyShell.OnNavigated for
        // ShellContent routes, or AppOnPageAppearing as a fallback) consumes these instead of
        // resolving fresh instances from DI.
        var subscriptions = new List<IDisposable>(pins.Count);
        foreach (var pin in pins)
            subscriptions.Add(configurator.EnqueueResolved(pin.Type, pin.Instance));

        try
        {
            if (allowLinuxDirectPush && OperatingSystem.IsLinux())
            {
                // Shell.GoToAsync is unreliable on Platform.Maui.Linux.Gtk4 - resolve
                // the page from the registered route map and push directly.
                var route = NavigationUri.GetTargetRoute(uri);
                var pageType = route == null ? null : navBuilder.GetPageTypeForRoute(route);
                if (pageType != null && services.GetService(pageType) is Page page)
                {
                    await shell.Navigation.PushAsync(page, true).ConfigureAwait(true);
                    return;
                }
            }

            // iOS Shell crashes (IndexOutOfRangeException in ShellSectionRenderer.InsertViewController)
            // when it inserts the earlier pages of a multi-page push while the last one is still
            // animating in - the page it inserts before is not in the navigation controller yet.
            var animate = !(OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst()) || !NavigationUri.PushesSeveralPages(uri);
            await shell.GoToAsync(uri, animate, parameters).ConfigureAwait(true);
        }
        catch
        {
            // Only roll back the pinned entries when navigation actually failed. On success we
            // leave them pinned because the apply sites have not fired yet (typically the next
            // dispatcher tick on Android) and disposing would fall back to a fresh DI resolve.
            foreach (var subscription in subscriptions)
                subscription.Dispose();

            // The Navigating event never arrived to consume the flag, and leaving it set would
            // wave the next user-driven navigation straight past every guard.
            this.isProgrammaticNavigation = false;
            throw;
        }
    }


    public Task<bool> NavigateTo(
        string route,
        bool relativeNavigation = true,
        bool bypassInterceptors = false,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    )
        => this.RunNavigation(new NavigationRequest(
            relativeNavigation ? route : $"//{route}",
            relativeNavigation ? NavigationType.Push : NavigationType.SetRoot,
            args.ToDictionary(x => x.Key, x => x.Value)
        )
        {
            // Nothing is resolved yet, so the pipeline builds the destination ViewModel when an
            // interceptor is actually there to look at it.
            ResolveViewModel = true,
            AllowLinuxDirectPush = true,
            BypassInterceptors = bypassInterceptors,
            CancellationToken = cancellationToken
        });


    public Task<bool> NavigateTo<TViewModel>(
        Action<TViewModel>? configure = null,
        bool relativeNavigation = true,
        bool bypassInterceptors = false,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    )
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        if (!relativeNavigation)
            route = $"//{route}";

        // Resolve and configure the viewmodel synchronously. Pin the instance on the configurator
        // so whichever apply site fires (ShinyRouteFactory.GetOrCreate for registered routes,
        // ShinyShell.OnNavigated for ShellContent routes, or AppOnPageAppearing as a fallback)
        // consumes our instance instead of resolving a fresh one from DI. The configure callback
        // runs first so every downstream hook - interceptors included, then
        // IPageLifecycleAware.OnAppearing on whatever schedule Shell decides - observes a fully
        // initialised viewmodel.
        //
        // Firing the navigation itself is left to ExecuteNavigation. GoToAsync throws
        // synchronously for an unknown route, which surfaces as the awaited Task's exception - the
        // only failure mode the navigator needs to report. We deliberately don't probe
        // Shell.Current.CurrentPage afterwards: on Android the awaiter can resolve before Shell's
        // CurrentItem chain updates and before Shell.OnNavigated / PageAppearing fire, so a
        // post-await BindingContext check races against Shell's own scheduling.
        var vm = (TViewModel)services.GetRequiredService(typeof(TViewModel)!);
        configure?.Invoke(vm);

        return this.RunNavigation(new NavigationRequest(
            route,
            relativeNavigation ? NavigationType.Push : NavigationType.SetRoot,
            args.ToDictionary(x => x.Key, x => x.Value)
        )
        {
            Pins = [new PinnedViewModel(typeof(TViewModel), vm!)],
            ViewModel = vm,
            ViewModelType = typeof(TViewModel),
            BypassInterceptors = bypassInterceptors,
            CancellationToken = cancellationToken
        });
    }


    /// <summary>
    /// Navigation core shared with <see cref="AppLinkRouter"/>. The ViewModel arrives already
    /// resolved and populated, so this only hands it to the same interception-and-pin path as
    /// <see cref="NavigateTo{TViewModel}"/> - which is what puts inbound links behind the app's
    /// <see cref="INavigationInterceptor"/>s, and keeps them off a second navigation path that
    /// would have to rediscover Android's timing behaviour.
    /// </summary>
    /// <param name="viewModelType">The ViewModel type to pin for the apply sites.</param>
    /// <param name="viewModel">The populated ViewModel instance.</param>
    /// <param name="route">An absolute ("//"-prefixed) or relative route.</param>
    /// <param name="source">App link or shortcut, for the observers.</param>
    internal Task<bool> NavigateToAppLink(Type viewModelType, object viewModel, string route, NavigationSource source)
        => this.RunNavigation(new NavigationRequest(
            route,
            NavigationUri.GetNavigationType(route),
            new Dictionary<string, object>()
        )
        {
            Pins = [new PinnedViewModel(viewModelType, viewModel)],
            ViewModel = viewModel,
            ViewModelType = viewModelType,
            Source = source
        });


    public async Task<DialogResult<T>> ShowDialog<TViewModel, T>(
        Action<TViewModel>? configure = null,
        CancellationToken cancellationToken = default
    ) where TViewModel : class, IDialogAware<T>
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pageType = navBuilder.GetPageTypeForViewModel(typeof(TViewModel))
            ?? throw new InvalidOperationException(
                $"Could not find a page mapped to viewmodel '{typeof(TViewModel)}'. Dialog viewmodels must be mapped with ShinyAppBuilder.Add<TPage, TViewModel>() or [ShellMap<TPage>] + AddGeneratedMaps()"
            );

        var vm = (TViewModel)services.GetRequiredService(typeof(TViewModel));
        var completion = new TaskCompletionSource<DialogResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);

        // TrySetResult throughout: the first of the two events wins, so a viewmodel that raises
        // Completed twice, or raises Cancelled after Completed, cannot corrupt the result.
        void OnCompleted(object? sender, T value) => completion.TrySetResult(DialogResult<T>.Complete(value));
        void OnCancelled(object? sender, EventArgs args) => completion.TrySetResult(DialogResult<T>.Cancel());

        // Subscribe before configure so a viewmodel that completes during configuration - or
        // synchronously from OnAppearing before the presenter's push has been awaited - is captured.
        vm.Completed += OnCompleted;
        vm.Cancelled += OnCancelled;
        PresentedDialog? observed = null;
        try
        {
            configure?.Invoke(vm);

            // Resolve and bind the page on the main thread - InitializeComponent is not safe to run
            // off it. Normal navigation gets this for free because Shell constructs the page itself
            // inside GoToAsync; here we own the instance so we own the dispatch.
            var page = await mainThread
                .InvokeOnMainThreadAsync(() =>
                {
                    var resolved = (Page)services.GetRequiredService(pageType);
                    resolved.BindingContext = vm;
                    return Task.FromResult(resolved);
                })
                .ConfigureAwait(false);

            var presenter = services.GetRequiredService<IDialogPresenter>();
            using var dismiss = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            logger.LogDebug("[Dialog] Presenting '{type}'", typeof(TViewModel));
            observed = dialogObservers.Create(vm, page, completion);
            var presentation = presenter.Present(page, vm, dismiss.Token);
            dialogObservers.Shown(observed);
            var finished = await Task.WhenAny(completion.Task, presentation).ConfigureAwait(false);

            if (finished == presentation)
            {
                // The presentation ended without the viewmodel producing a result - the user
                // dismissed it (back button, swipe-down, tap-outside). Awaiting it first surfaces a
                // genuine presenter fault instead of silently reporting cancellation.
                await presentation.ConfigureAwait(false);
                completion.TrySetResult(DialogResult<T>.Cancel());
            }
            else
            {
                // The viewmodel produced a result - tell the presenter to tear the dialog down and
                // wait until it is actually gone before returning to the caller.
                await dismiss.CancelAsync().ConfigureAwait(false);
                await presentation.ConfigureAwait(false);
            }

            // Caller cancellation is an OperationCanceledException, not a cancelled DialogResult -
            // the two mean different things and callers handle them differently.
            cancellationToken.ThrowIfCancellationRequested();

            var result = await completion.Task.ConfigureAwait(false);
            logger.LogDebug("[Dialog] '{type}' closed (cancelled: {cancelled})", typeof(TViewModel), result.IsCancelled);
            dialogObservers.Closed(observed, result.IsCancelled, result.Value);
            return result;
        }
        catch (Exception ex)
        {
            dialogObservers.Closed(observed, true, null, ex);
            throw;
        }
        finally
        {
            vm.Completed -= OnCompleted;
            vm.Cancelled -= OnCancelled;
        }
    }


    public Task<bool> PopToRoot(params IEnumerable<(string Key, object Value)> args)
        => this.PopToRoot(false, default, args);


    public Task<bool> PopToRoot(
        bool bypassInterceptors,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    )
    {
        // we already have 1 page covered and we don't want to pop the last page
        var count = Shell.Current.Navigation.NavigationStack.Count - 1;
        if (count < 1)
            count = 1;

        return this.DoGoBack(count, NavigationType.PopToRoot, bypassInterceptors, cancellationToken, args);
    }


    public Task<bool> GoBack(params IEnumerable<(string Key, object Value)> args)
        => this.DoGoBack(1, NavigationType.GoBack, false, default, args);


    public Task<bool> GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args)
        => this.DoGoBack(backCount, NavigationType.GoBack, false, default, args);


    public Task<bool> GoBack(
        int backCount,
        bool bypassInterceptors,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    )
        => this.DoGoBack(backCount, NavigationType.GoBack, bypassInterceptors, cancellationToken, args);


    public async Task SwitchShell(Shell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        if (application is not Application app)
            throw new InvalidOperationException($"Invalid MAUI Application - {application.GetType()}");

        var currentShell = Shell.Current;
        var parameters = new Dictionary<string, object>();
        var observed = this.StartObserving(NavigationSource.Navigator, NavigationType.SwitchShell, shell.GetType().Name, parameters);
        try
        {
            await this.DoSwitchShell(app, currentShell, shell, parameters).ConfigureAwait(false);
            observers.Finish(observed, NavigationOutcome.Completed, shell.GetType().Name);
        }
        catch (Exception ex)
        {
            observers.Finish(observed, NavigationOutcome.Failed, shell.GetType().Name, exception: ex);
            throw;
        }
    }


    async Task DoSwitchShell(Application app, Shell? currentShell, Shell shell, Dictionary<string, object> parameters)
    {
        if (currentShell?.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        if (currentShell != null)
        {
            this.RaiseNavigating(
                currentShell,
                shell.GetType().Name,
                NavigationType.SwitchShell,
                parameters
            );
        }

        if (app.Windows.Count == 0)
            throw new InvalidOperationException("No active window to switch Shell on");

        // Two-phase swap: first replace the current Shell with a temporary blank page.
        // This forces the platform to tear down the old Shell handlers and puts the
        // native window (UIWindow on iOS) into a clean state — avoiding the crash in
        // ShellFlyoutRenderer.ViewDidLoad that occurs when a new Shell handler is
        // created while the old Shell's native view hierarchy is still active.
        await mainThread.InvokeOnMainThreadAsync(() =>
        {
            var window = app.Windows[0];
            if (window.Page?.Handler is IElementHandler oldHandler)
            {
                logger.LogDebug("Disconnecting old handler '{type}'", oldHandler.GetType().Name);
                oldHandler.DisconnectHandler();
            }
            window.Page = new ContentPage();
        });

        // Yield to let the platform fully process the interim page and clean up native state
        await Task.Delay(50).ConfigureAwait(false);

        // Now set the actual Shell in a clean window state
        await mainThread.InvokeOnMainThreadAsync(() =>
        {
            var window = app.Windows[0];
            window.Page = shell;
            logger.LogDebug("Switched Shell to '{type}'", shell.GetType().Name);
        });
    }


    public Task SwitchShell<TShell>() where TShell : Shell
    {
        var shell = services.GetRequiredService<TShell>();
        return this.SwitchShell(shell);
    }


    public Task SetTabBadge(string route, int value) => tabBadgeManager.Set(route, value);


    public Task SetTabBadge<TViewModel>(int value)
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        return tabBadgeManager.Set(route, value);
    }


    public Task ClearTabBadge(string route) => tabBadgeManager.Clear(route);


    public Task ClearTabBadge<TViewModel>()
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        return tabBadgeManager.Clear(route);
    }


    Task<bool> DoGoBack(
        int backCount,
        NavigationType navType,
        bool bypassInterceptors,
        CancellationToken cancellationToken,
        IEnumerable<(string Key, object Value)> args
    )
    {
        if (backCount < 1)
            throw new ArgumentException("Back count must be 1 or more");

        var uri = String.Join("/", Enumerable.Repeat("..", backCount));
        var parameters = args.ToDictionary(x => x.Key, x => x.Value);

        return mainThread.InvokeOnMainThreadAsync(() =>
        {
            // Going back lands on a page that already exists, so interceptors get the ViewModel
            // off the navigation stack rather than a freshly built one - and it is never pinned.
            var stack = Shell.Current.Navigation.NavigationStack;
            var index = stack.Count - 1 - backCount;
            var targetViewModel = index >= 0 && index < stack.Count
                ? stack[index]?.BindingContext
                : null;

            return this.RunNavigationCore(new NavigationRequest(uri, navType, parameters)
            {
                ViewModel = targetViewModel,
                ViewModelType = targetViewModel?.GetType(),
                BypassInterceptors = bypassInterceptors,
                CancellationToken = cancellationToken
            });
        });
    }
    
    
    void AppOnDescendantAdded(object? sender, ElementEventArgs args)
    {
        if (args.Element is Shell shell)
        {
            // Detach first - DescendantAdded can fire again for a Shell that is re-parented, and
            // a doubled handler would ask every guard twice.
            shell.Navigating -= this.OnShellNavigating;
            shell.Navigating += this.OnShellNavigating;
            shell.Navigated -= this.OnShellNavigated;
            shell.Navigated += this.OnShellNavigated;
        }
    }


    /// <summary>
    /// The guard for navigation Shiny did not start - a tab tap, a flyout item, the hardware back
    /// button. <see cref="INavigationConfirmation"/> on the page being left is asked first (it
    /// owns the "may I leave" question), then the interceptors get their say on the destination.
    /// </summary>
    async void OnShellNavigating(object? sender, ShellNavigatingEventArgs shellArgs)
    {
        if (this.isProgrammaticNavigation)
        {
            this.isProgrammaticNavigation = false;
            return;
        }

        if (sender is not Shell shell)
            return;

        var vm = shell.CurrentPage?.BindingContext;
        var targetUri = shellArgs.Target?.Location?.ToString();
        var navType = ToNavigationType(shellArgs.Source);

        // Reported before anything decides, and without a deferral - observing must not change
        // how an app without interceptors navigates.
        var observed = observers.HasObservers
            ? observers.StartShell(
                navType,
                targetUri ?? String.Empty,
                shellArgs.Current?.Location?.ToString(),
                vm,
                shellArgs.Source
            )
            : null;

        // A navigation Shell will not let anyone cancel has no deferral to give (GetDeferral
        // returns null), so there is nothing for a guard to decide - only to observe.
        var hasInterceptors = interceptors.HasInterceptors;
        if (!shellArgs.CanCancel || (vm is not INavigationConfirmation && !hasInterceptors))
        {
            this.WatchForShellCancellation(shell, shellArgs, observed);
            return;
        }

        // Anything awaited past this point needs the deferral, or Shell completes the navigation
        // underneath us.
        var deferral = shellArgs.GetDeferral();
        NavigationRequest? redirect = null;
        IReadOnlyList<NavigationRedirect> redirects = [];
        try
        {
            if (vm is INavigationConfirmation confirm && !await confirm.CanNavigate())
            {
                shellArgs.Cancel();
                observers.Finish(observed, NavigationOutcome.Cancelled, targetUri ?? String.Empty, cancelledBy: confirm);
                return;
            }

            if (!hasInterceptors)
                return;

            var uri = targetUri;
            if (String.IsNullOrWhiteSpace(uri))
                return;

            var parameters = new Dictionary<string, object>();

            // ResolveViewModel is false here: Shell is building this destination itself, so
            // constructing a ViewModel for the interceptors would create one nobody binds.
            var interception = await interceptors
                .Run(uri, navType, parameters)
                .ConfigureAwait(true);

            if (interception.IsCancelled)
            {
                shellArgs.Cancel();
                observers.Finish(
                    observed,
                    NavigationOutcome.Cancelled,
                    interception.Uri,
                    interception.Redirects,
                    interception.CancelledBy
                );
                return;
            }

            if (interception.IsRedirected)
            {
                // Shell's own navigation is abandoned and reissued as ours, which is what lets the
                // redirect target get a pinned, interceptor-visible ViewModel. The observers hear
                // about it once the reissued navigation lands, not when Shell's is cancelled.
                observers.ReleaseShell(observed);
                redirects = interception.Redirects;
                shellArgs.Cancel();
                redirect = new NavigationRequest(
                    interception.Uri,
                    NavigationUri.GetNavigationType(interception.Uri),
                    parameters
                )
                {
                    Pins = this.CanPinResolved(interception, interception.Uri)
                        ? [new PinnedViewModel(interception.ViewModelType!, interception.ViewModel!)]
                        : []
                };
            }
        }
        catch (Exception ex)
        {
            // A throwing guard must not let the navigation through, and there is no caller to
            // report to on this path - Shell raised the event.
            logger.LogError(ex, "[Navigation] Interception of '{uri}' failed - navigation cancelled", shellArgs.Target?.Location);
            shellArgs.Cancel();
            observers.Finish(observed, NavigationOutcome.Failed, targetUri ?? String.Empty, exception: ex);
        }
        finally
        {
            deferral.Complete();

            // Every exit but a redirect leaves Shell to finish the navigation - including the early
            // returns above, which is why this is here and not after the block. Harmless when the
            // navigation was already finished or handed over: ShellCancelled ignores it then.
            this.WatchForShellCancellation(shell, shellArgs, observed);
        }

        if (redirect == null)
            return;

        // Posted rather than issued inline: Shell is still unwinding the navigation we just
        // cancelled, and starting the next one on top of that is how you get a Shell that has
        // moved but thinks it hasn't. The interceptors have already approved this destination, so
        // it goes straight to ExecuteNavigation rather than round the pipeline again.
        mainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await this.ExecuteNavigation(redirect.Uri, redirect.NavigationType, redirect.Parameters, redirect.Pins, false);
                observers.Finish(observed, NavigationOutcome.Redirected, redirect.Uri, redirects);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Navigation] Redirect to '{uri}' failed", redirect.Uri);
                observers.Finish(observed, NavigationOutcome.Failed, redirect.Uri, redirects, exception: ex);
            }
        });
    }


    /// <summary>
    /// A Shell-driven navigation Shiny let through can still be cancelled by Shell or by another
    /// <see cref="Shell.Navigating"/> handler, and Shell raises nothing when that happens. Checking
    /// on the next dispatcher turn catches every synchronous cancel; a deferred one is caught when
    /// the next Shell-driven navigation starts (see <see cref="NavigationObservers.StartShell"/>).
    /// </summary>
    void WatchForShellCancellation(Shell shell, ShellNavigatingEventArgs args, NavigationStarted? observed)
    {
        if (observed == null)
            return;

        shell.Dispatcher?.Dispatch(() =>
        {
            if (args.Cancelled)
                observers.ShellCancelled(observed);
        });
    }


    void OnShellNavigated(object? sender, ShellNavigatedEventArgs args)
    {
        if (observers.HasObservers)
            observers.ShellNavigated(args.Current?.Location?.ToString());
    }


    static NavigationType ToNavigationType(ShellNavigationSource source) => source switch
    {
        ShellNavigationSource.Pop => NavigationType.GoBack,
        ShellNavigationSource.PopToRoot => NavigationType.PopToRoot,
        ShellNavigationSource.ShellItemChanged or
        ShellNavigationSource.ShellSectionChanged or
        ShellNavigationSource.ShellContentChanged => NavigationType.SetRoot,
        _ => NavigationType.Push
    };
    
    
    void AppOnDescendantRemoved(object? sender, ElementEventArgs args)
    {
        if (args.Element is Page { BindingContext: IDisposable disposable })
        {
            logger.LogDebug("[Dispose] ViewModel '{type}'", disposable.GetType());
            disposable.Dispose();
        }
    }

    
    void AppOnPageAppearing(object? sender, Page page)
    {
        // BindingContext may be inherited from Shell rather than explicitly set —
        // check whether it's already the correct ViewModel type
        var viewModelType = navBuilder.GetViewModelTypeForPage(page);
        if (viewModelType != null && (page.BindingContext == null || !viewModelType.IsInstanceOfType(page.BindingContext)))
        {
            // Prefer the pinned instance from a pending NavigateTo<TVm> /
            // INavigationBuilder.Navigate call. Falls back to DI for the
            // initial-page case where no programmatic navigation issued one.
            var vm = configurator.TryConsume(viewModelType) ?? services.GetService(viewModelType);
            page.BindingContext = vm;
            logger.LogDebug("[Binding] ViewModel {type} set on page", viewModelType);
        }

        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnAppearing] ViewModel '{type}' ", lc.GetType());
            lc.OnAppearing();
        }

        tabBadgeManager.ReapplyAll();
        this.RaiseNavigated(page.BindingContext);
    }


    void AppOnPageDisappearing(object? sender, Page page)
    {
        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnDisappearing] ViewModel '{type}' ", lc.GetType());
            lc.OnDisappearing();
        }
    }
}
