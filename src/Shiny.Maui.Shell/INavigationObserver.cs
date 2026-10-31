namespace Shiny;


/// <summary>
/// Where a navigation came from.
/// </summary>
public enum NavigationSource
{
    /// <summary>A call on <see cref="INavigator"/> - route, typed, builder, back, pop to root, or a Shell swap.</summary>
    Navigator,

    /// <summary>
    /// Navigation Shell started on its own - a tab or flyout item tapped, the back button or back
    /// gesture. <see cref="NavigationStarted.ShellSource"/> says which.
    /// </summary>
    Shell,

    /// <summary>An inbound app link (deep link / universal link).</summary>
    AppLink,

    /// <summary>A home screen quick action.</summary>
    Shortcut
}


/// <summary>
/// How a navigation ended.
/// </summary>
public enum NavigationOutcome
{
    /// <summary>The navigation reached its destination.</summary>
    Completed,

    /// <summary>
    /// Something said no - an <see cref="INavigationInterceptor"/>, the page's
    /// <see cref="INavigationConfirmation"/>, or (for Shell-driven navigation) Shell itself or
    /// another handler of <see cref="Shell.Navigating"/>. See <see cref="NavigationFinished.CancelledBy"/>.
    /// </summary>
    Cancelled,

    /// <summary>
    /// An interceptor sent the navigation somewhere else, and it got there.
    /// <see cref="NavigationFinished.Uri"/> is where it ended up; <see cref="NavigationFinished.Redirects"/> is how.
    /// </summary>
    Redirected,

    /// <summary>
    /// The navigation threw - an interceptor failed, Shell rejected the route, or the caller's
    /// <see cref="CancellationToken"/> fired (<see cref="NavigationFinished.Exception"/> is then an
    /// <see cref="OperationCanceledException"/>).
    /// </summary>
    Failed
}


/// <summary>
/// One hop of a redirect chain.
/// </summary>
/// <param name="FromUri">The destination that was abandoned.</param>
/// <param name="ToUri">Where the interceptor sent the navigation instead.</param>
/// <param name="Interceptor">The interceptor that redirected.</param>
public sealed record NavigationRedirect(
    string FromUri,
    string ToUri,
    INavigationInterceptor Interceptor
);


/// <summary>
/// A navigation that has started - before any <see cref="INavigationInterceptor"/> has run.
/// </summary>
/// <param name="Id">Unique for the life of the app. The matching <see cref="NavigationFinished"/> carries the same instance.</param>
/// <param name="Source">Who started it.</param>
/// <param name="NavigationType">How the destination is reached.</param>
/// <param name="Uri">The requested destination. For Shell-driven navigation, Shell's target location.</param>
/// <param name="FromUri">Shell's location when the navigation started, or null when there is no Shell yet.</param>
/// <param name="FromViewModel">The ViewModel of the page being left.</param>
/// <param name="Parameters">The navigation arguments, as they were when the navigation started.</param>
/// <param name="ShellSource">For <see cref="NavigationSource.Shell"/>, what Shell says it was doing - eg. <see cref="ShellNavigationSource.ShellSectionChanged"/> for a tab tap, <see cref="ShellNavigationSource.Pop"/> for back.</param>
/// <param name="StartedAt">When the navigation started.</param>
public sealed record NavigationStarted(
    long Id,
    NavigationSource Source,
    NavigationType NavigationType,
    string Uri,
    string? FromUri,
    object? FromViewModel,
    IReadOnlyDictionary<string, object> Parameters,
    ShellNavigationSource? ShellSource,
    DateTimeOffset StartedAt
)
{
    /// <summary>Which way through the stack this navigation goes.</summary>
    public NavigationDirection Direction => this.NavigationType.GetDirection();

    /// <summary>Monotonic start time, for <see cref="NavigationFinished.Duration"/>.</summary>
    internal long Timestamp { get; init; }
}


/// <summary>
/// How a navigation ended. Every <see cref="NavigationStarted"/> is followed by exactly one of these.
/// </summary>
/// <param name="Navigation">The navigation this finishes.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Uri">
/// The destination it settled on: the redirect target after a redirect, Shell's location after a
/// completed Shell-driven navigation, otherwise the requested URI. For a cancelled navigation this
/// is the destination that was refused - the user is still at <see cref="NavigationStarted.FromUri"/>.
/// </param>
/// <param name="Redirects">Every redirect the interceptors made, in order. Empty when there were none.</param>
/// <param name="CancelledBy">
/// For <see cref="NavigationOutcome.Cancelled"/>: the <see cref="INavigationInterceptor"/> that cancelled,
/// or the <see cref="INavigationConfirmation"/> ViewModel that refused. Null when Shell, or code
/// outside Shiny, cancelled a Shell-driven navigation.
/// </param>
/// <param name="Exception">For <see cref="NavigationOutcome.Failed"/>: what was thrown.</param>
/// <param name="Duration">Time from start to finish, interceptors included.</param>
public sealed record NavigationFinished(
    NavigationStarted Navigation,
    NavigationOutcome Outcome,
    string Uri,
    IReadOnlyList<NavigationRedirect> Redirects,
    object? CancelledBy,
    Exception? Exception,
    TimeSpan Duration
);


/// <summary>
/// Watches every navigation the app makes without taking part in it. Unlike an
/// <see cref="INavigationInterceptor"/>, an observer cannot change a navigation, and registering
/// one does not make Shiny run its interceptor pipeline - an app with no interceptors navigates
/// exactly as it did before.
/// </summary>
/// <remarks>
/// Observers see navigation from every source: <see cref="INavigator"/> calls (including the
/// fluent builder, back, pop to root and <c>SwitchShell</c>), app links, home screen shortcuts, and
/// navigation Shell starts itself - tab and flyout taps, the back button and gesture. Each
/// navigation produces one <see cref="OnNavigationStarted"/> followed by one
/// <see cref="OnNavigationFinished"/>. <c>ShowDialog</c> is not navigation and is not reported.
///
/// <para>
/// Register with <see cref="ShinyAppBuilder.AddNavigationObserver{TObserver}"/>, or add an
/// <see cref="INavigationObserver"/> to the service collection directly - which is how a library
/// can observe an app it does not configure. Observers are resolved once, on the first navigation.
/// </para>
///
/// <para>
/// Callbacks run synchronously, usually on the main thread, in the middle of navigation: keep them
/// fast and hand anything slow off elsewhere. An exception thrown by an observer is logged and
/// never affects the navigation.
/// </para>
///
/// <para>
/// A Shell-driven navigation that Shiny lets through is finished when Shell raises
/// <see cref="Shell.Navigated"/>. If Shell, or a handler outside Shiny, cancels it instead, it is
/// finished as <see cref="NavigationOutcome.Cancelled"/> on the next dispatcher turn - or, if the
/// cancellation was deferred, when the next Shell-driven navigation starts.
/// </para>
/// </remarks>
public interface INavigationObserver
{
    /// <summary>A navigation has started; no interceptor has run yet.</summary>
    void OnNavigationStarted(NavigationStarted navigation) { }

    /// <summary>A navigation has finished.</summary>
    void OnNavigationFinished(NavigationFinished navigation) { }
}
