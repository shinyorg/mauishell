using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Shiny;


public sealed class ShinyAppBuilder(MauiAppBuilder builder)
{
    public MauiAppBuilder MauiBuilder => builder;
    
    readonly Dictionary<string, (bool RegisterRoute, Type PageType, Type ViewModelType)> typeMap = new();
    // Tuple elements can't carry [DynamicallyAccessedMembers], so the DI registrations are captured
    // while TPage/TViewModel are still annotated generics rather than replayed from typeMap's Types.
    readonly Dictionary<string, Action<IServiceCollection>> registrations = new();
    readonly List<(string Template, Type ViewModelType, Func<object, IReadOnlyDictionary<string, string>, bool> Apply)> appLinks = new();
    readonly List<(string? Id, Type ViewModelType, string Title, string? Subtitle, string? Icon, int Order, Action<object>? Configure)> appShortcuts = new();
    Action<AppLinkOptions>? appLinkOptions;

    /// <summary>
    /// Maps the Page <=> ViewModel and optionally registers the route
    /// </summary>
    /// <typeparam name="TPage">The page type</typeparam>
    /// <typeparam name="TViewModel">The viewmodel type</typeparam>
    /// <param name="route">Optional - uses page name otherwise</param>
    /// <param name="registerRoute">If you have datatemplate item configured in your Shell XAML, pass false here</param>
    /// <returns></returns>
    public ShinyAppBuilder Add<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage, 
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel
    >(string? route = null, bool registerRoute = true)
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged
    {
        route ??= typeof(TPage).Name;
        this.typeMap[route] = (registerRoute, typeof(TPage), typeof(TViewModel));
        this.registrations[route] = services =>
        {
            services.AddTransient<TPage>();
            services.AddTransient<TViewModel>();
        };
        return this;
    }


    /// <summary>
    /// Sets the dialog provider you want to use
    /// </summary>
    /// <typeparam name="TDialog"></typeparam>
    /// <returns></returns>
    public ShinyAppBuilder UseDialogs<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDialog
    >() where TDialog : class, IDialogs
    {
        builder.Services.AddSingleton<IDialogs, TDialog>();
        return this;
    }


    /// <summary>
    /// Sets the presenter used to display dialog ViewModels shown with
    /// <see cref="INavigator.ShowDialog{TViewModel, T}"/>. Defaults to
    /// <see cref="ShellModalDialogPresenter"/>, which pushes the page onto Shell's modal stack.
    /// </summary>
    /// <typeparam name="TPresenter"></typeparam>
    /// <returns></returns>
    public ShinyAppBuilder UseDialogPresenter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPresenter
    >() where TPresenter : class, IDialogPresenter
    {
        builder.Services.AddSingleton<IDialogPresenter, TPresenter>();
        return this;
    }


    /// <summary>
    /// Registers a navigation guard. Interceptors run in registration order on every navigation -
    /// <see cref="INavigator"/> calls, app links, shortcuts, and user-driven Shell navigation -
    /// and the first one to cancel or redirect wins.
    /// </summary>
    /// <remarks>Ordering is the interceptor's own business - override <see cref="INavigationInterceptor.Order"/>.</remarks>
    /// <typeparam name="TInterceptor">The interceptor. Registered as a singleton; register it yourself for any other lifetime.</typeparam>
    public ShinyAppBuilder AddNavigationInterceptor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInterceptor
    >() where TInterceptor : class, INavigationInterceptor
    {
        builder.Services.AddSingleton<INavigationInterceptor, TInterceptor>();
        return this;
    }


    /// <summary>
    /// Registers an already-constructed navigation guard.
    /// </summary>
    public ShinyAppBuilder AddNavigationInterceptor(INavigationInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);

        builder.Services.AddSingleton(interceptor);
        return this;
    }


    /// <summary>
    /// Registers a navigation guard written inline - enough for logging or a one-line rule,
    /// without a class for it.
    /// </summary>
    /// <param name="interceptor">Receives the destination URI, its ViewModel and the navigation's token, and returns what should happen.</param>
    /// <param name="order">Lowest runs first; ties keep registration order. The class equivalent is <see cref="INavigationInterceptor.Order"/>.</param>
    public ShinyAppBuilder AddNavigationInterceptor(
        Func<string, object?, CancellationToken, Task<NavigationInterceptorResult>> interceptor,
        int order = 0
    )
    {
        ArgumentNullException.ThrowIfNull(interceptor);

        builder.Services.AddSingleton<INavigationInterceptor>(new DelegateNavigationInterceptor(interceptor, order));
        return this;
    }


    /// <summary>
    /// Optional tuning for inbound app links. App links declared through the <c>appLinks</c>
    /// argument of <see cref="ShellMapAttribute{TPage}"/> are installed automatically by
    /// <c>AddGeneratedMaps()</c> - this is only needed to change the defaults.
    /// </summary>
    /// <param name="configure">See <see cref="AppLinkOptions"/>.</param>
    public ShinyAppBuilder UseAppLinks(Action<AppLinkOptions>? configure = null)
    {
        this.appLinkOptions = configure;
        return this;
    }


    /// <summary>
    /// Replaces how quick action titles and subtitles are resolved, so they can be pulled from
    /// resources instead of the attribute literals. Resolution runs at install time and on
    /// <see cref="IAppShortcuts.Refresh"/>.
    /// </summary>
    public ShinyAppBuilder UseShortcutText<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TText
    >() where TText : class, IAppShortcutText
    {
        builder.Services.AddSingleton<IAppShortcutText, TText>();
        return this;
    }


    /// <summary>
    /// Registers a home screen quick action that navigates to the route mapped to
    /// <typeparamref name="TViewModel"/>. The generated <c>AddGeneratedMaps()</c> calls this for
    /// every <c>[ShellMap(Shortcut = "...")]</c> - call it directly when not using source generation,
    /// or when the route needs values a declared shortcut cannot supply.
    /// </summary>
    /// <param name="title">Shown on the quick action. Both platforms truncate hard.</param>
    /// <param name="subtitle">Secondary line. iOS only; most Android launchers ignore it.</param>
    /// <param name="icon">Platform icon name - a system icon on iOS, a drawable on Android.</param>
    /// <param name="order">Display order.</param>
    /// <param name="id">Defaults to the route. Give an explicit id for two shortcuts to one route.</param>
    /// <param name="configure">
    /// Populates the ViewModel on activation. Only the id is persisted by the platform, so this is
    /// re-registered every launch rather than serialized - which is why a lambda works here.
    /// </param>
    public ShinyAppBuilder AddAppShortcut<TViewModel>(
        string title,
        string? subtitle = null,
        string? icon = null,
        int order = 0,
        string? id = null,
        Action<TViewModel>? configure = null
    ) where TViewModel : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        this.appShortcuts.Add((
            id,
            typeof(TViewModel),
            title,
            subtitle,
            icon,
            order,
            configure == null ? null : vm => configure((TViewModel)vm)
        ));
        return this;
    }


    /// <summary>
    /// Registers a single app link template against a ViewModel. Called by the source-generated
    /// <c>AddGeneratedMaps()</c> - there is no need to call it by hand.
    /// </summary>
    /// <param name="template">The URL template, eg. <c>"product/{id}"</c>.</param>
    /// <param name="apply">
    /// Binds the extracted values onto the ViewModel, returning false when a required value is
    /// missing or unparseable so the router can try the next matching template.
    /// </param>
    public ShinyAppBuilder AddAppLink<TViewModel>(
        string template,
        Func<TViewModel, IReadOnlyDictionary<string, string>, bool> apply
    ) where TViewModel : class
    {
        this.appLinks.Add((template, typeof(TViewModel), (vm, values) => apply((TViewModel)vm, values)));
        return this;
    }


    /// <summary>
    /// Gets the route registration for a route name - the page and ViewModel types plus whether
    /// the route was registered with Shell (false means it is declared in AppShell XAML).
    /// </summary>
    public (bool RegisterRoute, Type PageType, Type ViewModelType)? GetRouteInfo(string route)
        => this.typeMap.TryGetValue(route, out var entry) ? entry : null;


    /// <summary>
    /// Gets the ViewModel type for a given page type
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    public Type? GetViewModelTypeForPage(Page page)
    {
        var pageType = page.GetType();
        foreach (var pair in this.typeMap)
        {
            if (pair.Value.PageType == pageType) 
                return pair.Value.ViewModelType;
        }
        return null;
    }


    /// <summary>
    /// Gets the route for a given ViewModel type
    /// </summary>
    /// <param name="viewModelType"></param>
    /// <returns></returns>
    public string? GetRouteForViewModel(Type viewModelType)
    {
        foreach (var pair in this.typeMap)
        {
            if (pair.Value.ViewModelType == viewModelType)
                return pair.Key;
        }

        return null;
    }


    /// <summary>
    /// Gets the Page type mapped to a given ViewModel type
    /// </summary>
    /// <param name="viewModelType"></param>
    /// <returns></returns>
    public Type? GetPageTypeForViewModel(Type viewModelType)
    {
        foreach (var pair in this.typeMap)
        {
            if (pair.Value.ViewModelType == viewModelType)
                return pair.Value.PageType;
        }

        return null;
    }


    /// <summary>
    /// Gets the Page type registered for a given route
    /// </summary>
    /// <param name="route"></param>
    /// <returns></returns>
    public Type? GetPageTypeForRoute(string route)
        => this.typeMap.TryGetValue(route, out var entry) ? entry.PageType : null;
    
    
    internal void RegisterDependencies()
    {
        foreach (var pair in this.typeMap)
        {
            this.registrations[pair.Key](builder.Services);

            if (pair.Value.RegisterRoute)
            {
                Routing.RegisterRoute(
                    pair.Key,
                    new ShinyRouteFactory(
                        pair.Value.PageType,
                        pair.Value.ViewModelType
                    )
                );
            }
        }

        this.RegisterAppLinks();
        this.RegisterAppShortcuts();
    }


    void RegisterAppLinks()
    {
        if (this.appLinks.Count == 0)
            return;

        var registry = new AppLinkRegistry();
        foreach (var link in this.appLinks)
        {
            // The route and its push-vs-reset behaviour come from the type map, so templates and
            // Add<TPage, TViewModel>() can be registered in any order.
            var route = this.GetRouteForViewModel(link.ViewModelType);
            if (route == null)
                throw new InvalidOperationException(
                    $"App link template '{link.Template}' targets '{link.ViewModelType}', which is not mapped to a page. Map it with ShinyAppBuilder.Add<TPage, TViewModel>() or [ShellMap<TPage>]."
                );

            var info = this.GetRouteInfo(route)!.Value;
            registry.Add(new RegisteredAppLink(
                link.Template,
                route,
                link.ViewModelType,
                info.RegisterRoute,
                link.Apply
            ));
        }

        var options = new AppLinkOptions();
        this.appLinkOptions?.Invoke(options);

        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<AppLinkRouter>();
        builder.Services.AddSingleton<IAppLinks>(sp => sp.GetRequiredService<AppLinkRouter>());
        builder.Services.AddSingleton<IMauiInitializeService>(sp => sp.GetRequiredService<AppLinkRouter>());

        // Declaring a template is the opt-in. Installing the platform hooks here rather than
        // behind a second call removes a whole class of "declared it but nothing happens".
        AppLinkLifecycle.Register(builder);
    }


    void RegisterAppShortcuts()
    {
        if (this.appShortcuts.Count == 0)
            return;

        var registry = new AppShortcutRegistry();
        foreach (var entry in this.appShortcuts)
        {
            var route = this.GetRouteForViewModel(entry.ViewModelType);
            if (route == null)
                throw new InvalidOperationException(
                    $"App shortcut '{entry.Title}' targets '{entry.ViewModelType}', which is not mapped to a page. Map it with ShinyAppBuilder.Add<TPage, TViewModel>() or [ShellMap<TPage>]."
                );

            var info = this.GetRouteInfo(route)!.Value;
            registry.Add(new RegisteredAppShortcut(
                entry.Id ?? route,
                route,
                entry.ViewModelType,
                info.RegisterRoute,
                entry.Title,
                entry.Subtitle,
                entry.Icon,
                entry.Order,
                entry.Configure
            ));
        }

        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton<AppShortcutRouter>();
        builder.Services.AddSingleton<IAppShortcuts>(sp => sp.GetRequiredService<AppShortcutRouter>());
        builder.Services.TryAddSingleton(new AppLinkOptions());
        builder.Services.TryAddSingleton<IAppShortcutText, DeclaredAppShortcutText>();

        // Hand the declared set to MAUI and let it own platform delivery. The activation callback
        // resolves services lazily because the builder has no container yet.
        builder.ConfigureEssentials(essentials =>
        {
            // The declared strings are the baseline. This delegate runs during Build(), before any
            // service provider exists, so text cannot be resolved here - a custom IAppShortcutText
            // is applied by AppShortcutTextInitializer once the app is up.
            foreach (var shortcut in registry.Shortcuts)
                essentials.AddAppAction(shortcut.Id, shortcut.Title, shortcut.Subtitle, shortcut.Icon);

            essentials.OnAppAction(action =>
            {
                var services = IPlatformApplication.Current?.Services;
                var router = services?.GetService<AppShortcutRouter>();
                if (router == null)
                {
                    // A tapped shortcut doing nothing with no explanation is the worst outcome
                    // here, so say so rather than returning quietly.
                    services
                        ?.GetService<ILoggerFactory>()
                        ?.CreateLogger<ShinyAppBuilder>()
                        .LogError("[Shortcut] '{id}' activated but AppShortcutRouter could not be resolved", action.Id);
                    return;
                }
                _ = router.Handle(action.Id);
            });
        });

        // Only pay for a second push when the app actually localizes - the default provider
        // returns the declared strings, which are already installed.
        builder.Services.AddSingleton<IMauiInitializeService, AppShortcutTextInitializer>();

        if (registry.Shortcuts.Count > AppShortcutRegistry.PlatformMaximum)
        {
            builder.Services.AddSingleton<IMauiInitializeService>(
                new AppShortcutCapWarning(registry.Shortcuts.Count)
            );
        }
    }


    /// <summary>
    /// Re-pushes the shortcut set through <see cref="IAppShortcutText"/> at startup, but only when
    /// a custom provider is registered - the default returns what is already installed.
    /// </summary>
    sealed class AppShortcutTextInitializer : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
        {
            if (services.GetService<IAppShortcutText>() is null or DeclaredAppShortcutText)
                return;

            var shortcuts = services.GetService<IAppShortcuts>();
            if (shortcuts != null)
                _ = shortcuts.Refresh();
        }
    }


    /// <summary>
    /// The compile-time SHINY011 equivalent for hand-registered shortcuts, which get no diagnostic.
    /// Worth saying out loud because the platform drops the excess silently.
    /// </summary>
    sealed class AppShortcutCapWarning(int count) : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
            => services
                .GetService<ILoggerFactory>()
                ?.CreateLogger<ShinyAppBuilder>()
                .LogWarning(
                    "{count} app shortcuts are registered but at most {max} are shown - the rest are dropped silently by the platform",
                    count,
                    AppShortcutRegistry.PlatformMaximum
                );
    }


}