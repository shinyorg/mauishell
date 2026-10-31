using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// <see cref="INavigationObserver"/> reporting. The bookkeeping lives in
/// <see cref="NavigationObservers"/>, which needs no Shell, so it is tested for real; the
/// navigator's cancel and failure paths finish before Shell is touched, so those are driven
/// end-to-end through <see cref="ShinyShellNavigator"/> too.
/// </summary>
/// <remarks>
/// Not covered headless: a navigation that reaches <c>Shell.GoToAsync</c> (Completed/Redirected
/// from the navigator) and the Shell-driven handler (tab, flyout, back), both of which need a live
/// Shell. Their wiring is locked at source level in <see cref="NavigatorContractTests"/>.
/// </remarks>
public class NavigationObserverTests
{
    class DetailPage : ContentPage { }
    class LoginPage : ContentPage { }

    class TestViewModel : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }

    class DetailViewModel : TestViewModel { }
    class LoginViewModel : TestViewModel { }


    class RecordingObserver : INavigationObserver
    {
        public List<NavigationStarted> Started { get; } = new();
        public List<NavigationFinished> Finished { get; } = new();

        public void OnNavigationStarted(NavigationStarted navigation) => this.Started.Add(navigation);
        public void OnNavigationFinished(NavigationFinished navigation) => this.Finished.Add(navigation);
    }


    class ThrowingObserver : INavigationObserver
    {
        public void OnNavigationStarted(NavigationStarted navigation) => throw new InvalidOperationException("observer");
        public void OnNavigationFinished(NavigationFinished navigation) => throw new InvalidOperationException("observer");
    }


    /// <summary>Implements nothing - the interface's default methods must be enough.</summary>
    class EmptyObserver : INavigationObserver { }


    class DelegateInterceptor(Func<string, NavigationInterceptorResult> handler) : INavigationInterceptor
    {
        public Task<NavigationInterceptorResult> InterceptNavigationAsync(string uri, object? viewModel, CancellationToken cancellationToken)
            => Task.FromResult(handler(uri));
    }


    /// <summary>Runs everything inline - there is no UI thread in a test.</summary>
    class InlineMainThread : IMainThread
    {
        public Task InvokeOnMainThreadAsync(Action action) { action(); return Task.CompletedTask; }
        public Task InvokeOnMainThreadAsync(Func<Task> func) => func();
        public Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> func) => func();
        public void BeginInvokeOnMainThread(Action action) => action();
    }


    static NavigationObservers BuildObservers(params INavigationObserver[] observers)
    {
        var services = new ServiceCollection();
        foreach (var observer in observers)
            services.AddSingleton(observer);

        return new NavigationObservers(NullLogger<NavigationObservers>.Instance, services.BuildServiceProvider());
    }


    static (ShinyShellNavigator Navigator, RecordingObserver Observer) BuildNavigator(params INavigationInterceptor[] interceptors)
    {
        var appBuilder = new ShinyAppBuilder(MauiApp.CreateBuilder(false));
        appBuilder.Add<DetailPage, DetailViewModel>("detail");
        appBuilder.Add<LoginPage, LoginViewModel>("login");

        var observer = new RecordingObserver();
        var services = new ServiceCollection();
        services.AddTransient<DetailViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<INavigationObserver>(observer);
        foreach (var interceptor in interceptors)
            services.AddSingleton(interceptor);
        var sp = services.BuildServiceProvider();

        var mainThread = new InlineMainThread();
        var navigator = new ShinyShellNavigator(
            NullLogger<ShinyShellNavigator>.Instance,
            mainThread,
            appBuilder,
            new ShellTabBadgeManager(NullLogger<ShellTabBadgeManager>.Instance, mainThread),
            new ShellNavigationConfigurator(),
            new NavigationInterceptorPipeline(
                NullLogger<NavigationInterceptorPipeline>.Instance,
                sp,
                appBuilder,
                new NavigationContextAccessor()
            ),
            new NavigationObservers(NullLogger<NavigationObservers>.Instance, sp),
            new DialogObservers(NullLogger<DialogObservers>.Instance, sp)
        );
        return (navigator, observer);
    }


    #region NavigationObservers

    [Fact]
    public void NoObservers_ReportsNothingAndReturnsNull()
    {
        var observers = BuildObservers();

        observers.HasObservers.ShouldBeFalse();
        observers.Start(NavigationSource.Navigator, NavigationType.Push, "detail", null, null, null).ShouldBeNull();
        observers.StartShell(NavigationType.SetRoot, "//tab2", "//tab1", null, ShellNavigationSource.ShellSectionChanged).ShouldBeNull();

        // Everything downstream accepts the null without complaint.
        observers.Finish(null, NavigationOutcome.Completed, "detail");
        observers.ShellNavigated("//tab2");
        observers.ShellCancelled(null);
        observers.ReleaseShell(null);
    }


    [Fact]
    public void Start_ReportsEverythingAboutTheNavigation()
    {
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var from = new object();

        var started = observers.Start(
            NavigationSource.AppLink,
            NavigationType.Push,
            "detail",
            new Dictionary<string, object> { ["Id"] = 42 },
            "//home",
            from
        );

        started.ShouldNotBeNull();
        observer.Started.Single().ShouldBeSameAs(started);
        started.Source.ShouldBe(NavigationSource.AppLink);
        started.NavigationType.ShouldBe(NavigationType.Push);
        started.Direction.ShouldBe(NavigationDirection.Forward);
        started.Uri.ShouldBe("detail");
        started.FromUri.ShouldBe("//home");
        started.FromViewModel.ShouldBeSameAs(from);
        started.Parameters["Id"].ShouldBe(42);
        started.ShellSource.ShouldBeNull();
        observers.InFlight.ShouldContain(started);
    }


    [Fact]
    public void Start_CopiesTheParameters()
    {
        // The navigator hands the same dictionary to INavigationAware.OnNavigatingFrom, which may
        // add to it - the observer must see the parameters as they were when navigation started.
        var observers = BuildObservers(new RecordingObserver());
        var parameters = new Dictionary<string, object> { ["Id"] = 1 };

        var started = observers.Start(NavigationSource.Navigator, NavigationType.Push, "detail", parameters, null, null)!;
        parameters["Added"] = true;

        started.Parameters.ContainsKey("Added").ShouldBeFalse();
    }


    [Fact]
    public void Ids_AreUnique()
    {
        var observers = BuildObservers(new RecordingObserver());

        var one = observers.Start(NavigationSource.Navigator, NavigationType.Push, "a", null, null, null)!;
        var two = observers.Start(NavigationSource.Navigator, NavigationType.Push, "b", null, null, null)!;

        one.Id.ShouldNotBe(two.Id);
    }


    [Fact]
    public void Finish_ReportsOnceOnly()
    {
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var started = observers.Start(NavigationSource.Navigator, NavigationType.Push, "detail", null, null, null);

        observers.Finish(started, NavigationOutcome.Completed, "detail");
        observers.Finish(started, NavigationOutcome.Failed, "detail", exception: new Exception());

        var finished = observer.Finished.Single();
        finished.Navigation.ShouldBeSameAs(started);
        finished.Outcome.ShouldBe(NavigationOutcome.Completed);
        finished.Redirects.ShouldBeEmpty();
        finished.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
        observers.InFlight.ShouldBeEmpty();
    }


    [Fact]
    public void Finish_CarriesTheOutcomeDetail()
    {
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var interceptor = new DelegateInterceptor(_ => NavigationInterceptorResult.Continue);
        var redirect = new NavigationRedirect("detail", "//login", interceptor);
        var started = observers.Start(NavigationSource.Navigator, NavigationType.Push, "detail", null, null, null);

        observers.Finish(started, NavigationOutcome.Cancelled, "//login", [redirect], interceptor);

        var finished = observer.Finished.Single();
        finished.Outcome.ShouldBe(NavigationOutcome.Cancelled);
        finished.Uri.ShouldBe("//login");
        finished.Redirects.ShouldBe([redirect]);
        finished.CancelledBy.ShouldBeSameAs(interceptor);
        finished.Exception.ShouldBeNull();
    }


    [Fact]
    public void ThrowingObserver_DoesNotStopTheOthers()
    {
        var recording = new RecordingObserver();
        var observers = BuildObservers(new ThrowingObserver(), recording);

        var started = observers.Start(NavigationSource.Navigator, NavigationType.Push, "detail", null, null, null);
        observers.Finish(started, NavigationOutcome.Completed, "detail");

        recording.Started.Count.ShouldBe(1);
        recording.Finished.Count.ShouldBe(1);
    }


    [Fact]
    public void ObserverImplementingNothing_IsValid()
    {
        var observers = BuildObservers(new EmptyObserver());

        var started = observers.Start(NavigationSource.Navigator, NavigationType.Push, "detail", null, null, null);
        observers.Finish(started, NavigationOutcome.Completed, "detail");

        started.ShouldNotBeNull();
    }


    [Fact]
    public void ShellNavigation_CompletesWhenShellNavigates()
    {
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var vm = new object();

        var started = observers.StartShell(NavigationType.SetRoot, "//main/tab2", "//main/tab1", vm, ShellNavigationSource.ShellSectionChanged)!;
        started.Source.ShouldBe(NavigationSource.Shell);
        started.ShellSource.ShouldBe(ShellNavigationSource.ShellSectionChanged);
        started.FromViewModel.ShouldBeSameAs(vm);
        observer.Finished.ShouldBeEmpty();

        observers.ShellNavigated("//main/tab2/content");

        var finished = observer.Finished.Single();
        finished.Navigation.ShouldBeSameAs(started);
        finished.Outcome.ShouldBe(NavigationOutcome.Completed);
        // Where Shell says it landed, not where it was asked to go.
        finished.Uri.ShouldBe("//main/tab2/content");
    }


    [Fact]
    public void ShellNavigated_WithNothingPending_ReportsNothing()
    {
        // Shell also raises Navigated for Shiny's own GoToAsync - that is already reported by the
        // navigator and must not finish anything here.
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);

        observers.ShellNavigated("//detail");

        observer.Finished.ShouldBeEmpty();
    }


    [Fact]
    public void ShellCancelled_FinishesThePendingNavigationAsCancelled()
    {
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var started = observers.StartShell(NavigationType.GoBack, "..", "//main/detail", null, ShellNavigationSource.Pop);

        observers.ShellCancelled(started);

        var finished = observer.Finished.Single();
        finished.Outcome.ShouldBe(NavigationOutcome.Cancelled);
        finished.Uri.ShouldBe("..");
        // Nothing in Shiny refused it.
        finished.CancelledBy.ShouldBeNull();

        // And a later Navigated does not finish it again.
        observers.ShellNavigated("//main");
        observer.Finished.Count.ShouldBe(1);
    }


    [Fact]
    public void ShellCancelled_AfterShinyFinishedIt_ReportsNothingMore()
    {
        // The confirmation/interceptor paths finish the navigation themselves, then the
        // cancellation watcher sees Shell's Cancelled flag - that must not report twice.
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var started = observers.StartShell(NavigationType.SetRoot, "//tab2", "//tab1", null, ShellNavigationSource.ShellSectionChanged);

        observers.Finish(started, NavigationOutcome.Cancelled, "//tab2", cancelledBy: "confirmation");
        observers.ShellCancelled(started);

        observer.Finished.Single().CancelledBy.ShouldBe("confirmation");
    }


    [Fact]
    public void ReleasedShellNavigation_IsNotFinishedByShell()
    {
        // A redirect hands the navigation over to Shiny; Shell cancelling its own original, and
        // then navigating for Shiny's reissue, must not finish it - Shiny does that when the
        // redirect lands.
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var started = observers.StartShell(NavigationType.SetRoot, "//admin", "//home", null, ShellNavigationSource.ShellItemChanged);

        observers.ReleaseShell(started);
        observers.ShellCancelled(started);
        observers.ShellNavigated("//login");
        observer.Finished.ShouldBeEmpty();

        observers.Finish(started, NavigationOutcome.Redirected, "//login");
        observer.Finished.Single().Outcome.ShouldBe(NavigationOutcome.Redirected);
    }


    [Fact]
    public void NextShellNavigation_FinishesAnAbandonedOneAsCancelled()
    {
        // A handler outside Shiny deferred and then cancelled - Shell raises nothing, so the next
        // Shell navigation is the first sign the previous one never happened.
        var observer = new RecordingObserver();
        var observers = BuildObservers(observer);
        var first = observers.StartShell(NavigationType.SetRoot, "//tab2", "//tab1", null, ShellNavigationSource.ShellSectionChanged);

        var second = observers.StartShell(NavigationType.SetRoot, "//tab3", "//tab1", null, ShellNavigationSource.ShellSectionChanged);

        var finished = observer.Finished.Single();
        finished.Navigation.ShouldBeSameAs(first);
        finished.Outcome.ShouldBe(NavigationOutcome.Cancelled);

        observers.ShellNavigated("//tab3");
        observer.Finished.Count.ShouldBe(2);
        observer.Finished[1].Navigation.ShouldBeSameAs(second);
        observer.Finished[1].Outcome.ShouldBe(NavigationOutcome.Completed);
    }

    #endregion

    #region Navigator

    [Fact]
    public async Task Navigator_CancelledByInterceptor_ReportsWhoCancelled()
    {
        var guard = new DelegateInterceptor(_ => NavigationInterceptorResult.Cancel());
        var (navigator, observer) = BuildNavigator(guard);

        var navigated = await navigator.NavigateTo("detail", args: [("Id", 7)]);

        navigated.ShouldBeFalse();
        var started = observer.Started.Single();
        started.Source.ShouldBe(NavigationSource.Navigator);
        started.Uri.ShouldBe("detail");
        started.NavigationType.ShouldBe(NavigationType.Push);
        started.Parameters["Id"].ShouldBe(7);

        var finished = observer.Finished.Single();
        finished.Navigation.ShouldBeSameAs(started);
        finished.Outcome.ShouldBe(NavigationOutcome.Cancelled);
        finished.CancelledBy.ShouldBeSameAs(guard);
    }


    [Fact]
    public async Task Navigator_RedirectThenCancel_ReportsTheChain()
    {
        var redirector = new DelegateInterceptor(uri => uri == "detail"
            ? NavigationInterceptorResult.Redirect("//login")
            : NavigationInterceptorResult.Continue);
        var blocker = new DelegateInterceptor(uri => uri == "//login"
            ? NavigationInterceptorResult.Cancel()
            : NavigationInterceptorResult.Continue);
        var (navigator, observer) = BuildNavigator(redirector, blocker);

        (await navigator.NavigateTo("detail")).ShouldBeFalse();

        var finished = observer.Finished.Single();
        finished.Outcome.ShouldBe(NavigationOutcome.Cancelled);
        finished.Uri.ShouldBe("//login");
        finished.CancelledBy.ShouldBeSameAs(blocker);
        var redirect = finished.Redirects.Single();
        redirect.FromUri.ShouldBe("detail");
        redirect.ToUri.ShouldBe("//login");
        redirect.Interceptor.ShouldBeSameAs(redirector);
    }


    [Fact]
    public async Task Navigator_InterceptorThrows_ReportsFailedAndStillThrows()
    {
        var (navigator, observer) = BuildNavigator(
            new DelegateInterceptor(_ => throw new InvalidOperationException("guard failed"))
        );

        await Should.ThrowAsync<InvalidOperationException>(() => navigator.NavigateTo("detail", relativeNavigation: false));

        observer.Started.Single().NavigationType.ShouldBe(NavigationType.SetRoot);
        var finished = observer.Finished.Single();
        finished.Outcome.ShouldBe(NavigationOutcome.Failed);
        finished.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("guard failed");
    }


    [Fact]
    public async Task Navigator_CallerCancellation_ReportsFailedWithOperationCanceled()
    {
        var (navigator, observer) = BuildNavigator(new DelegateInterceptor(_ => NavigationInterceptorResult.Continue));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => navigator.NavigateTo("detail", cancellationToken: cts.Token));

        var finished = observer.Finished.Single();
        finished.Outcome.ShouldBe(NavigationOutcome.Failed);
        finished.Exception.ShouldBeAssignableTo<OperationCanceledException>();
    }


    [Fact]
    public async Task Navigator_BuilderNavigation_IsObserved()
    {
        var (navigator, observer) = BuildNavigator(new DelegateInterceptor(_ => NavigationInterceptorResult.Cancel()));

        (await navigator.CreateBuilder(fromRoot: true).Add("login").Navigate()).ShouldBeFalse();

        observer.Started.Single().Uri.ShouldBe("//login");
        observer.Finished.Single().Outcome.ShouldBe(NavigationOutcome.Cancelled);
    }

    #endregion
}
