namespace Shiny;

public interface INavigator
{
    event EventHandler<NavigationEventArgs>? Navigating;
    event EventHandler<NavigatedEventArgs>? Navigated;

    /// <summary>
    /// Creates a fluent navigation builder for constructing multi-segment navigation URIs
    /// </summary>
    /// <param name="fromRoot">If true, builds an absolute URI starting with "//". If false (default), builds a relative URI.</param>
    INavigationBuilder CreateBuilder(bool fromRoot = false);


    /// <summary>
    /// Navigates to the specified route and passes the provided arguments to the target page or view model.
    /// Use this method with AI tooling along with the extension GetGeneratedRouteInfo
    /// </summary>
    /// <remarks>To receive the arguments passed via <paramref name="args"/>, the target page or view model
    /// must implement the <see cref="IQueryAttributable"/> interface.</remarks>
    /// <param name="route">The route to navigate to. This should be a valid route string recognized by the navigation system.</param>
    /// <param name="relativeNavigation">Assumes relative navigation (page1/page2), if set to false, assumes root navigation "//" </param>
    /// <param name="bypassInterceptors">Skips the registered <see cref="INavigationInterceptor"/>s - for the navigation a guard itself performs, which must not be guarded again.</param>
    /// <param name="cancellationToken">Passed to the interceptors; cancelling abandons the navigation with an <see cref="OperationCanceledException"/>.</param>
    /// <param name="args">A collection of key-value pairs representing the arguments to pass to the target page or view model. Each key
    /// must be unique.</param>
    /// <returns>True when the navigation happened; false when an <see cref="INavigationInterceptor"/> cancelled it. A redirected navigation returns true - it happened, somewhere else.</returns>
    Task<bool> NavigateTo(
        string route,
        bool relativeNavigation = true,
        bool bypassInterceptors = false,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    );


    /// <summary>
    /// Navigates to a view associated with the specified view model type.
    /// </summary>
    /// <remarks>This method allows for flexible navigation by enabling both view model configuration and the
    /// passing of additional arguments. Ensure that the specified view model type is properly registered and that any
    /// required arguments are provided.</remarks>
    /// <typeparam name="TViewModel">The type of the view model to navigate to. The view model must be registered in the navigation system.</typeparam>
    /// <param name="configure">An optional action to configure the view model before navigation. This can be used to set up properties or
    /// perform initialization.</param>
    /// <param name="relativeNavigation">Assumes relative navigation (page1/page2), if set to false, assumes root navigation "//" </param>
    /// <param name="bypassInterceptors">Skips the registered <see cref="INavigationInterceptor"/>s - for the navigation a guard itself performs, which must not be guarded again.</param>
    /// <param name="cancellationToken">Passed to the interceptors; cancelling abandons the navigation with an <see cref="OperationCanceledException"/>.</param>
    /// <param name="args">A collection of key-value pairs representing arguments to pass to the view during navigation. Each key must be
    /// unique.</param>
    /// <returns>True when the navigation happened; false when an <see cref="INavigationInterceptor"/> cancelled it.</returns>
    Task<bool> NavigateTo<TViewModel>(
        Action<TViewModel>? configure = null, 
        bool relativeNavigation = true,
        bool bypassInterceptors = false,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    );


    /// <summary>
    /// Presents the page mapped to <typeparamref name="TViewModel"/> as a dialog and asynchronously
    /// returns the value the ViewModel produces.
    /// </summary>
    /// <remarks>
    /// The ViewModel must implement <see cref="IDialogAware{T}"/> and, like any navigable ViewModel,
    /// must be mapped to a page - via <c>ShinyAppBuilder.Add&lt;TPage, TViewModel&gt;()</c> or
    /// <c>[ShellMap&lt;TPage&gt;]</c> + <c>AddGeneratedMaps()</c>.
    ///
    /// How the dialog appears is decided by the registered <see cref="IDialogPresenter"/>, which
    /// defaults to a Shell modal push. Every dismissal path completes the returned task: if the user
    /// dismisses the dialog without the ViewModel raising either event, the result is a cancellation.
    ///
    /// The source generator emits a typed <c>Show{Route}Dialog</c> extension for every dialog-aware
    /// <c>[ShellMap]</c> ViewModel, which infers both type arguments and surfaces
    /// <c>[ShellProperty]</c> values as method parameters - prefer that over calling this directly.
    /// </remarks>
    /// <typeparam name="TViewModel">The dialog ViewModel type.</typeparam>
    /// <typeparam name="T">The type of value the dialog returns.</typeparam>
    /// <param name="configure">An optional action to configure the ViewModel before it is presented.</param>
    /// <param name="cancellationToken">Dismisses the dialog and throws <see cref="OperationCanceledException"/>. Distinct from the user cancelling, which returns a cancelled <see cref="DialogResult{T}"/>.</param>
    /// <returns>The value the ViewModel produced, or a cancelled <see cref="DialogResult{T}"/>.</returns>
    Task<DialogResult<T>> ShowDialog<TViewModel, T>(
        Action<TViewModel>? configure = null,
        CancellationToken cancellationToken = default
    ) where TViewModel : class, IDialogAware<T>;


    /// <summary>
    /// Returns to the root page regardless of how far up the stack you are
    /// </summary>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns>True when the navigation happened; false when an <see cref="INavigationInterceptor"/> cancelled it.</returns>
    Task<bool> PopToRoot(params IEnumerable<(string Key, object Value)> args);


    /// <summary>
    /// <see cref="PopToRoot(IEnumerable{ValueTuple{string, object}})"/> with control over the interceptors.
    /// </summary>
    /// <param name="bypassInterceptors">Skips the registered <see cref="INavigationInterceptor"/>s.</param>
    /// <param name="cancellationToken">Passed to the interceptors.</param>
    /// <param name="args">Parameters to pass to the target.</param>
    Task<bool> PopToRoot(
        bool bypassInterceptors,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    );
    
    
    /// <summary>
    /// Navigates back to the previous view or state in the application, optionally passing parameters to the target.
    /// </summary>
    /// <remarks>The behavior of the navigation may depend on the application's navigation stack or state
    /// management. Ensure that the keys and values provided in <paramref name="args"/> are compatible with the target
    /// view or state.</remarks>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns>True when the navigation happened; false when an <see cref="INavigationInterceptor"/> cancelled it.</returns>
    Task<bool> GoBack(params IEnumerable<(string Key, object Value)> args);
    
    
    /// <summary>
    /// Navigates back to the previous view or state in the application, optionally passing parameters to the target.
    /// </summary>
    /// <remarks>The behavior of the navigation may depend on the application's navigation stack or state
    /// management. Ensure that the keys and values provided in <paramref name="args"/> are compatible with the target
    /// view or state.</remarks>
    /// <param name="backCount">Allows you to go back 1 or more pages in the navigation stack. Defaults to 1.</param>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns>True when the navigation happened; false when an <see cref="INavigationInterceptor"/> cancelled it.</returns>
    Task<bool> GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args);


    /// <summary>
    /// <see cref="GoBack(int, IEnumerable{ValueTuple{string, object}})"/> with control over the interceptors.
    /// </summary>
    /// <param name="backCount">Allows you to go back 1 or more pages in the navigation stack.</param>
    /// <param name="bypassInterceptors">Skips the registered <see cref="INavigationInterceptor"/>s.</param>
    /// <param name="cancellationToken">Passed to the interceptors.</param>
    /// <param name="args">Parameters to pass to the target.</param>
    Task<bool> GoBack(
        int backCount,
        bool bypassInterceptors,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    );


    /// <summary>
    /// Switches the application's main page to the specified Shell instance, replacing the current Shell entirely.
    /// </summary>
    /// <remarks>This replaces the current <see cref="Application.MainPage"/> with the provided Shell instance,
    /// effectively resetting the navigation stack. Use this for scenarios like switching between a login shell and a main app shell.</remarks>
    /// <param name="shell">The Shell instance to switch to.</param>
    /// <returns>A task that represents the asynchronous shell switch operation.</returns>
    Task SwitchShell(Shell shell);


    /// <summary>
    /// Switches the application's main page to a Shell instance resolved from the dependency injection container.
    /// </summary>
    /// <remarks>This resolves the specified Shell type from the service provider and replaces the current
    /// <see cref="Application.MainPage"/>, effectively resetting the navigation stack.</remarks>
    /// <typeparam name="TShell">The type of Shell to resolve and switch to. Must be registered in the DI container.</typeparam>
    /// <returns>A task that represents the asynchronous shell switch operation.</returns>
    Task SwitchShell<TShell>() where TShell : Shell;


    /// <summary>
    /// Sets the badge value on a tab in the active Shell.
    /// </summary>
    /// <param name="route">The Shell route for the tab to update.</param>
    /// <param name="value">The numeric badge value to display.</param>
    /// <returns>A task that represents the asynchronous badge update.</returns>
    Task SetTabBadge(string route, int value);


    /// <summary>
    /// Sets the badge value on a tab associated with the specified view model in the active Shell.
    /// </summary>
    /// <typeparam name="TViewModel">The view model mapped to the tab page.</typeparam>
    /// <param name="value">The numeric badge value to display.</param>
    /// <returns>A task that represents the asynchronous badge update.</returns>
    Task SetTabBadge<TViewModel>(int value);


    /// <summary>
    /// Sets the badge text on a tab in the active Shell. An empty string shows a dot badge.
    /// </summary>
    /// <remarks>Windows shows only numeric text as a number; other text is drawn as a dot.</remarks>
    /// <param name="route">The Shell route for the tab to update.</param>
    /// <param name="text">The badge text, or an empty string for a dot.</param>
    /// <returns>A task that represents the asynchronous badge update.</returns>
    Task SetTabBadge(string route, string text);


    /// <summary>
    /// Sets the badge text on a tab associated with the specified view model in the active Shell. An empty string shows a dot badge.
    /// </summary>
    /// <remarks>Windows shows only numeric text as a number; other text is drawn as a dot.</remarks>
    /// <typeparam name="TViewModel">The view model mapped to the tab page.</typeparam>
    /// <param name="text">The badge text, or an empty string for a dot.</param>
    /// <returns>A task that represents the asynchronous badge update.</returns>
    Task SetTabBadge<TViewModel>(string text);


    /// <summary>
    /// Clears the badge from a tab in the active Shell.
    /// </summary>
    /// <param name="route">The Shell route for the tab to update.</param>
    /// <returns>A task that represents the asynchronous badge update.</returns>
    Task ClearTabBadge(string route);


    /// <summary>
    /// Clears the badge from a tab associated with the specified view model in the active Shell.
    /// </summary>
    /// <typeparam name="TViewModel">The view model mapped to the tab page.</typeparam>
    /// <returns>A task that represents the asynchronous badge update.</returns>
    Task ClearTabBadge<TViewModel>();
}
