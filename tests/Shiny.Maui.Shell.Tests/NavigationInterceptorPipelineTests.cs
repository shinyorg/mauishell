using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// The interceptor chain, tested for real - the pipeline is the one part of navigation that needs
/// no Shell, which is exactly why cancellation, redirect and destination-ViewModel resolution live
/// in it rather than in the navigator.
/// </summary>
public class NavigationInterceptorPipelineTests
{
    class HomePage : ContentPage { }
    class LoginPage : ContentPage { }
    class DetailPage : ContentPage { }

    class TestViewModel : INotifyPropertyChanged
    {
#pragma warning disable CS0067 // required by ShinyAppBuilder.Add, never raised in these tests
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
        public string? Tag { get; set; }
    }

    class HomeViewModel : TestViewModel { }
    class LoginViewModel : TestViewModel { }
    class DetailViewModel : TestViewModel { }


    /// <summary>Records what it saw and returns whatever it was told to.</summary>
    class TestInterceptor(string name, Func<string, object?, NavigationInterceptorResult> handler, int order = 0)
        : INavigationInterceptor
    {
        public List<(string Uri, object? ViewModel)> Calls { get; } = new();
        public string Name => name;
        public CancellationToken LastToken { get; private set; }
        public int Order => order;

        public Task<NavigationInterceptorResult> InterceptNavigationAsync(
            string uri,
            object? viewModel,
            CancellationToken cancellationToken
        )
        {
            this.Calls.Add((uri, viewModel));
            this.LastToken = cancellationToken;
            return Task.FromResult(handler(uri, viewModel));
        }
    }


    static NavigationInterceptorPipeline Build(
        IEnumerable<INavigationInterceptor> interceptors,
        NavigationContextAccessor? accessor = null
    )
    {
        var appBuilder = new ShinyAppBuilder(MauiApp.CreateBuilder(false));
        appBuilder.Add<HomePage, HomeViewModel>("home");
        appBuilder.Add<LoginPage, LoginViewModel>("login");
        appBuilder.Add<DetailPage, DetailViewModel>("detail");

        var services = new ServiceCollection();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DetailViewModel>();
        foreach (var interceptor in interceptors)
            services.AddSingleton(interceptor);

        return new NavigationInterceptorPipeline(
            NullLogger<NavigationInterceptorPipeline>.Instance,
            services.BuildServiceProvider(),
            appBuilder,
            accessor ?? new NavigationContextAccessor()
        );
    }


    static Task<NavigationInterception> Run(
        NavigationInterceptorPipeline pipeline,
        string uri = "detail",
        object? viewModel = null,
        Type? viewModelType = null,
        bool resolveViewModel = false,
        CancellationToken cancellationToken = default
    ) => pipeline.Run(
        uri,
        NavigationUri.GetNavigationType(uri),
        new Dictionary<string, object>(),
        viewModel,
        viewModelType,
        resolveViewModel,
        cancellationToken
    );


    [Fact]
    public async Task NoInterceptors_PassesThroughUntouched()
    {
        var pipeline = Build([]);
        pipeline.HasInterceptors.ShouldBeFalse();

        var result = await Run(pipeline, "detail", resolveViewModel: true);

        result.IsCancelled.ShouldBeFalse();
        result.Uri.ShouldBe("detail");
        // Nothing is resolved when nobody is there to look at it.
        result.ViewModel.ShouldBeNull();
        result.IsViewModelResolved.ShouldBeFalse();
    }


    [Fact]
    public async Task RunsEveryInterceptorInRegistrationOrder()
    {
        var order = new List<string>();
        var one = new TestInterceptor("one", (_, _) => { order.Add("one"); return NavigationInterceptorResult.Continue; });
        var two = new TestInterceptor("two", (_, _) => { order.Add("two"); return NavigationInterceptorResult.Continue; });
        var pipeline = Build([one, two]);

        var result = await Run(pipeline);

        result.IsCancelled.ShouldBeFalse();
        order.ShouldBe(["one", "two"]);
    }


    [Fact]
    public async Task Cancel_StopsTheChain()
    {
        var second = new TestInterceptor("second", (_, _) => NavigationInterceptorResult.Continue);
        var pipeline = Build([
            new TestInterceptor("first", (_, _) => NavigationInterceptorResult.Cancel()),
            second
        ]);

        var result = await Run(pipeline);

        result.IsCancelled.ShouldBeTrue();
        second.Calls.ShouldBeEmpty();
    }


    [Fact]
    public async Task DestinationViewModel_IsResolvedForInterceptors()
    {
        var seen = new TestInterceptor("seen", (_, _) => NavigationInterceptorResult.Continue);
        var pipeline = Build([seen]);

        var result = await Run(pipeline, "detail", resolveViewModel: true);

        seen.Calls.Single().ViewModel.ShouldBeOfType<DetailViewModel>();
        result.ViewModelType.ShouldBe(typeof(DetailViewModel));
        // The navigator pins what the pipeline built, so an interceptor's mutations survive.
        result.IsViewModelResolved.ShouldBeTrue();
        result.ViewModel.ShouldBeSameAs(seen.Calls.Single().ViewModel);
    }


    [Fact]
    public async Task CallerSuppliedViewModel_IsHandedOverAndNotReResolved()
    {
        var vm = new DetailViewModel { Tag = "populated" };
        var seen = new TestInterceptor("seen", (_, _) => NavigationInterceptorResult.Continue);
        var pipeline = Build([seen]);

        var result = await Run(pipeline, "detail", vm, typeof(DetailViewModel));

        seen.Calls.Single().ViewModel.ShouldBeSameAs(vm);
        result.ViewModel.ShouldBeSameAs(vm);
        // The caller already owns the pinning decision for its own instance.
        result.IsViewModelResolved.ShouldBeFalse();
    }


    [Fact]
    public async Task UnmappedRoute_StillReachesInterceptorsWithoutAViewModel()
    {
        var seen = new TestInterceptor("seen", (_, _) => NavigationInterceptorResult.Continue);
        var pipeline = Build([seen]);

        var result = await Run(pipeline, "notmapped", resolveViewModel: true);

        seen.Calls.Single().ShouldBe(("notmapped", null));
        result.IsCancelled.ShouldBeFalse();
        result.ViewModel.ShouldBeNull();
    }


    [Fact]
    public async Task Redirect_RetargetsAndResolvesTheNewDestination()
    {
        var pipeline = Build([
            new TestInterceptor("guard", (uri, _) => uri == "detail"
                ? NavigationInterceptorResult.Redirect("/login")
                : NavigationInterceptorResult.Continue)
        ]);

        var result = await Run(pipeline, "detail", resolveViewModel: true);

        result.IsRedirected.ShouldBeTrue();
        // "/login" is promoted to the Shell absolute form.
        result.Uri.ShouldBe("//login");
        result.ViewModel.ShouldBeOfType<LoginViewModel>();
        result.IsViewModelResolved.ShouldBeTrue();
    }


    [Fact]
    public async Task RedirectByViewModelType_ResolvesTheRoute()
    {
        var pipeline = Build([
            new TestInterceptor("guard", (uri, _) => uri == "detail"
                ? NavigationInterceptorResult.Redirect<LoginViewModel>()
                : NavigationInterceptorResult.Continue)
        ]);

        var result = await Run(pipeline, "detail", resolveViewModel: true);

        result.Uri.ShouldBe("//login");
    }


    [Fact]
    public async Task RedirectByViewModelType_Relative_Pushes()
    {
        var pipeline = Build([
            new TestInterceptor("guard", (uri, _) => uri == "detail"
                ? NavigationInterceptorResult.Redirect<LoginViewModel>(relativeNavigation: true)
                : NavigationInterceptorResult.Continue)
        ]);

        (await Run(pipeline, "detail")).Uri.ShouldBe("login");
    }


    [Fact]
    public async Task Redirect_RestartsTheChainSoTheNewDestinationIsGuardedToo()
    {
        var logger = new TestInterceptor("logger", (_, _) => NavigationInterceptorResult.Continue);
        var pipeline = Build([
            new TestInterceptor("guard", (uri, _) => uri == "detail"
                ? NavigationInterceptorResult.Redirect("//login")
                : NavigationInterceptorResult.Continue),
            logger
        ]);

        await Run(pipeline, "detail", resolveViewModel: true);

        // The first pass never reached the logger - the guard broke out of it - and the second
        // pass showed it the redirect target with the target's own ViewModel.
        logger.Calls.Count.ShouldBe(1);
        logger.Calls[0].Uri.ShouldBe("//login");
        logger.Calls[0].ViewModel.ShouldBeOfType<LoginViewModel>();
    }


    [Fact]
    public async Task RedirectedDestination_CanStillBeCancelled()
    {
        var pipeline = Build([
            new TestInterceptor("guard", (uri, _) => uri == "detail"
                ? NavigationInterceptorResult.Redirect("//login")
                : NavigationInterceptorResult.Continue),
            new TestInterceptor("nologin", (uri, _) => uri == "//login"
                ? NavigationInterceptorResult.Cancel()
                : NavigationInterceptorResult.Continue)
        ]);

        (await Run(pipeline, "detail")).IsCancelled.ShouldBeTrue();
    }


    [Fact]
    public async Task RedirectToTheCurrentDestination_IsIgnoredRatherThanLooping()
    {
        // An unconditional "go to login" guard says this every time the user navigates to login.
        var pipeline = Build([
            new TestInterceptor("guard", (_, _) => NavigationInterceptorResult.Redirect("//login"))
        ]);

        var result = await Run(pipeline, "//login");

        result.IsCancelled.ShouldBeFalse();
        result.IsRedirected.ShouldBeFalse();
        result.Uri.ShouldBe("//login");
    }


    [Fact]
    public async Task RedirectLoop_Throws()
    {
        var pipeline = Build([
            new TestInterceptor("pingpong", (uri, _) => NavigationInterceptorResult.Redirect(
                uri == "//login" ? "//home" : "//login"
            ))
        ]);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Run(pipeline, "detail"));
        ex.Message.ShouldContain("redirect loop");
    }


    [Fact]
    public async Task InterceptorException_Propagates()
    {
        var pipeline = Build([
            new TestInterceptor("boom", (_, _) => throw new InvalidOperationException("boom"))
        ]);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Run(pipeline, "detail"));
        ex.Message.ShouldBe("boom");
    }


    [Fact]
    public async Task Context_IsVisibleToInterceptorsAndClearedAfterwards()
    {
        var accessor = new NavigationContextAccessor();
        NavigationContext? captured = null;
        var pipeline = Build(
            [new TestInterceptor("capture", (_, _) =>
            {
                captured = accessor.Current;
                return NavigationInterceptorResult.Continue;
            })],
            accessor
        );

        await pipeline.Run(
            "detail",
            NavigationType.Push,
            new Dictionary<string, object> { ["id"] = 5 },
            resolveViewModel: true
        );

        captured.ShouldNotBeNull();
        captured!.ToUri.ShouldBe("detail");
        captured.NavigationType.ShouldBe(NavigationType.Push);
        captured.Parameters["id"].ShouldBe(5);
        captured.RedirectCount.ShouldBe(0);
        accessor.Current.ShouldBeNull();
    }


    [Fact]
    public async Task Context_CountsRedirects()
    {
        var accessor = new NavigationContextAccessor();
        var counts = new List<int>();
        var pipeline = Build(
            [new TestInterceptor("guard", (uri, _) =>
            {
                counts.Add(accessor.Current!.RedirectCount);
                return uri == "detail" ? NavigationInterceptorResult.Redirect("//login") : NavigationInterceptorResult.Continue;
            })],
            accessor
        );

        await Run(pipeline, "detail");

        counts.ShouldBe([0, 1]);
    }

    [Fact]
    public async Task Order_BeatsRegistrationOrder()
    {
        var order = new List<string>();
        var pipeline = Build([
            new TestInterceptor("audit", (_, _) => { order.Add("audit"); return NavigationInterceptorResult.Continue; }, order: 100),
            new TestInterceptor("auth", (_, _) => { order.Add("auth"); return NavigationInterceptorResult.Continue; }, order: -100),
            new TestInterceptor("default", (_, _) => { order.Add("default"); return NavigationInterceptorResult.Continue; })
        ]);

        await Run(pipeline);

        order.ShouldBe(["auth", "default", "audit"]);
    }


    [Fact]
    public async Task EqualOrder_KeepsRegistrationOrder()
    {
        var order = new List<string>();
        var pipeline = Build([
            new TestInterceptor("one", (_, _) => { order.Add("one"); return NavigationInterceptorResult.Continue; }, order: 5),
            new TestInterceptor("two", (_, _) => { order.Add("two"); return NavigationInterceptorResult.Continue; }, order: 5)
        ]);

        await Run(pipeline);

        order.ShouldBe(["one", "two"]);
    }


    [Fact]
    public async Task CancellationToken_IsHandedToEveryInterceptor()
    {
        using var cts = new CancellationTokenSource();
        var seen = new TestInterceptor("seen", (_, _) => NavigationInterceptorResult.Continue);
        var pipeline = Build([seen]);

        await Run(pipeline, cancellationToken: cts.Token);

        seen.LastToken.ShouldBe(cts.Token);
    }


    [Fact]
    public async Task CancelledToken_AbandonsTheNavigation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var never = new TestInterceptor("never", (_, _) => NavigationInterceptorResult.Continue);
        var pipeline = Build([never]);

        await Should.ThrowAsync<OperationCanceledException>(() => Run(pipeline, cancellationToken: cts.Token));
        never.Calls.ShouldBeEmpty();
    }


    [Fact]
    public async Task Context_CarriesTheDirection()
    {
        var accessor = new NavigationContextAccessor();
        var directions = new List<NavigationDirection>();
        var pipeline = Build(
            [new TestInterceptor("capture", (_, _) =>
            {
                directions.Add(accessor.Current!.Direction);
                return NavigationInterceptorResult.Continue;
            })],
            accessor
        );

        await Run(pipeline, "detail");
        await Run(pipeline, "..");
        await Run(pipeline, "//main/home");

        directions.ShouldBe([NavigationDirection.Forward, NavigationDirection.Back, NavigationDirection.Root]);
    }


    [Fact]
    public async Task Cancel_ReportsWhichInterceptorCancelled()
    {
        var blocker = new TestInterceptor("blocker", (_, _) => NavigationInterceptorResult.Cancel());
        var pipeline = Build([
            new TestInterceptor("first", (_, _) => NavigationInterceptorResult.Continue),
            blocker
        ]);

        var result = await Run(pipeline);

        result.CancelledBy.ShouldBeSameAs(blocker);
        result.Redirects.ShouldBeEmpty();
    }


    [Fact]
    public async Task Continue_HasNoCancellerAndNoRedirects()
    {
        var pipeline = Build([new TestInterceptor("ok", (_, _) => NavigationInterceptorResult.Continue)]);

        var result = await Run(pipeline);

        result.CancelledBy.ShouldBeNull();
        result.Redirects.ShouldBeEmpty();
    }


    [Fact]
    public async Task Redirects_AreReportedInOrderWithTheInterceptorThatMadeEach()
    {
        var toLogin = new TestInterceptor("toLogin", (uri, _) => uri == "detail"
            ? NavigationInterceptorResult.Redirect("//login")
            : NavigationInterceptorResult.Continue);
        var toHome = new TestInterceptor("toHome", (uri, _) => uri == "//login"
            ? NavigationInterceptorResult.Redirect("//home")
            : NavigationInterceptorResult.Continue);
        var pipeline = Build([toLogin, toHome]);

        var result = await Run(pipeline, "detail");

        result.Uri.ShouldBe("//home");
        result.Redirects.Count.ShouldBe(2);
        result.Redirects[0].ShouldBe(new NavigationRedirect("detail", "//login", toLogin));
        result.Redirects[1].ShouldBe(new NavigationRedirect("//login", "//home", toHome));
    }


    [Fact]
    public async Task RedirectThenCancel_KeepsTheChainAndTheCanceller()
    {
        var redirector = new TestInterceptor("guard", (uri, _) => uri == "detail"
            ? NavigationInterceptorResult.Redirect("//login")
            : NavigationInterceptorResult.Continue);
        var blocker = new TestInterceptor("nologin", (uri, _) => uri == "//login"
            ? NavigationInterceptorResult.Cancel()
            : NavigationInterceptorResult.Continue);
        var pipeline = Build([redirector, blocker]);

        var result = await Run(pipeline, "detail");

        result.IsCancelled.ShouldBeTrue();
        result.CancelledBy.ShouldBeSameAs(blocker);
        result.Redirects.Single().Interceptor.ShouldBeSameAs(redirector);
    }


    [Fact]
    public async Task IgnoredSelfRedirect_IsNotReportedAsARedirect()
    {
        var pipeline = Build([
            new TestInterceptor("guard", (_, _) => NavigationInterceptorResult.Redirect("//login"))
        ]);

        (await Run(pipeline, "//login")).Redirects.ShouldBeEmpty();
    }
}
