using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// <see cref="IDialogObserver"/> and <see cref="PresentedDialog"/>. <c>ShowDialog</c> is driven
/// end-to-end through <see cref="ShinyShellNavigator"/> with a presenter that shows nothing, which
/// is the whole point of the presenter abstraction - only the on-screen part is left out.
/// </summary>
public class DialogObserverTests
{
    class PickPage : ContentPage { }
    class FlagPage : ContentPage { }

    class PickViewModel : INotifyPropertyChanged, IDialogAware<string>
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
        public event EventHandler<string>? Completed;
        public event EventHandler? Cancelled;

        public void Pick(string value) => this.Completed?.Invoke(this, value);
        public void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);
    }

    class FlagViewModel : INotifyPropertyChanged, IDialogAware<bool>
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<bool>? Completed;
        public event EventHandler? Cancelled;
#pragma warning restore CS0067
    }


    /// <summary>Shows nothing; stays "on screen" until dismissed, or until told the user swiped it away.</summary>
    class FakePresenter : IDialogPresenter
    {
        TaskCompletionSource? userDismissed;
        public Exception? Fault { get; set; }
        public Page? Page { get; private set; }

        public async Task Present(Page page, object viewModel, CancellationToken dismiss)
        {
            this.Page = page;
            if (this.Fault != null)
                throw this.Fault;

            this.userDismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var registration = dismiss.Register(() => this.userDismissed.TrySetResult());
            await this.userDismissed.Task;
        }

        public void UserDismiss() => this.userDismissed?.TrySetResult();
    }


    class RecordingObserver : IDialogObserver
    {
        public List<PresentedDialog> Shown { get; } = new();
        public List<(PresentedDialog Dialog, DialogClosed Result)> Closed { get; } = new();
        public Action<PresentedDialog>? OnShown { get; set; }

        public void OnDialogShown(PresentedDialog dialog)
        {
            this.Shown.Add(dialog);
            this.OnShown?.Invoke(dialog);
        }

        public void OnDialogClosed(PresentedDialog dialog, DialogClosed result) => this.Closed.Add((dialog, result));
    }


    class InlineMainThread : IMainThread
    {
        public Task InvokeOnMainThreadAsync(Action action) { action(); return Task.CompletedTask; }
        public Task InvokeOnMainThreadAsync(Func<Task> func) => func();
        public Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> func) => func();
        public void BeginInvokeOnMainThread(Action action) => action();
    }


    sealed record Fixture(
        ShinyShellNavigator Navigator,
        FakePresenter Presenter,
        RecordingObserver Observer,
        DialogObservers Dialogs
    );


    static Fixture Build(bool observe = true)
    {
        var appBuilder = new ShinyAppBuilder(MauiApp.CreateBuilder(false))
            .Add<PickPage, PickViewModel>("pick")
            .Add<FlagPage, FlagViewModel>("flag");

        var presenter = new FakePresenter();
        var observer = new RecordingObserver();
        var services = new ServiceCollection();
        services.AddTransient<PickPage>();
        services.AddTransient<PickViewModel>();
        services.AddTransient<FlagPage>();
        services.AddTransient<FlagViewModel>();
        services.AddSingleton<IDialogPresenter>(presenter);
        services.AddSingleton<IApplication>(new Application());
        if (observe)
            services.AddSingleton<IDialogObserver>(observer);
        var sp = services.BuildServiceProvider();

        var mainThread = new InlineMainThread();
        var dialogs = new DialogObservers(NullLogger<DialogObservers>.Instance, sp);
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
            dialogs
        );
        navigator.Initialize(sp);
        return new Fixture(navigator, presenter, observer, dialogs);
    }


    [Fact]
    public async Task ViewModelResult_IsReportedShownThenClosed()
    {
        var f = Build();
        f.Observer.OnShown = dialog => ((PickViewModel)dialog.ViewModel).Pick("red");

        var result = await f.Navigator.ShowDialog<PickViewModel, string>();

        result.Value.ShouldBe("red");
        var shown = f.Observer.Shown.Single();
        shown.ViewModelType.ShouldBe(typeof(PickViewModel));
        shown.ResultType.ShouldBe(typeof(string));
        shown.Page.ShouldBeSameAs(f.Presenter.Page);
        shown.IsOpen.ShouldBeFalse();

        var closed = f.Observer.Closed.Single();
        closed.Dialog.ShouldBeSameAs(shown);
        closed.Result.IsCancelled.ShouldBeFalse();
        closed.Result.Value.ShouldBe("red");
        closed.Result.Exception.ShouldBeNull();
        f.Dialogs.Open.ShouldBeEmpty();
    }


    [Fact]
    public async Task TryComplete_AnswersTheDialogForTheCaller()
    {
        var f = Build();
        var task = f.Navigator.ShowDialog<PickViewModel, string>();

        var dialog = f.Dialogs.Open.Single();
        dialog.IsOpen.ShouldBeTrue();
        dialog.TryComplete("blue").ShouldBeTrue();

        (await task).Value.ShouldBe("blue");
        f.Observer.Closed.Single().Result.Value.ShouldBe("blue");
        // The first result wins.
        dialog.TryComplete("green").ShouldBeFalse();
        dialog.TryCancel().ShouldBeFalse();
    }


    [Fact]
    public async Task TryCancel_CancelsTheDialog()
    {
        var f = Build();
        var task = f.Navigator.ShowDialog<PickViewModel, string>();

        f.Dialogs.Open.Single().TryCancel().ShouldBeTrue();

        (await task).IsCancelled.ShouldBeTrue();
        var closed = f.Observer.Closed.Single().Result;
        closed.IsCancelled.ShouldBeTrue();
        closed.Value.ShouldBeNull();
    }


    [Fact]
    public async Task TryComplete_WithTheWrongType_ThrowsAndLeavesTheDialogOpen()
    {
        var f = Build();
        var task = f.Navigator.ShowDialog<FlagViewModel, bool>();
        var dialog = f.Dialogs.Open.Single();

        Should.Throw<ArgumentException>(() => dialog.TryComplete("yes"));
        // A value type cannot be null either.
        Should.Throw<ArgumentException>(() => dialog.TryComplete(null));
        dialog.IsOpen.ShouldBeTrue();

        dialog.TryComplete(true).ShouldBeTrue();
        (await task).Value.ShouldBeTrue();
    }


    [Fact]
    public async Task TryComplete_WithNullForAReferenceType_IsAValue()
    {
        var f = Build();
        var task = f.Navigator.ShowDialog<PickViewModel, string>();

        f.Dialogs.Open.Single().TryComplete(null).ShouldBeTrue();

        var result = await task;
        result.IsCancelled.ShouldBeFalse();
        result.Value.ShouldBeNull();
    }


    [Fact]
    public async Task UserDismissal_IsReportedAsCancelled()
    {
        var f = Build();
        var task = f.Navigator.ShowDialog<PickViewModel, string>();

        f.Presenter.UserDismiss();

        (await task).IsCancelled.ShouldBeTrue();
        f.Observer.Closed.Single().Result.IsCancelled.ShouldBeTrue();
    }


    [Fact]
    public async Task CallerCancellation_IsReportedWithTheException()
    {
        var f = Build();
        using var cts = new CancellationTokenSource();
        var task = f.Navigator.ShowDialog<PickViewModel, string>(cancellationToken: cts.Token);

        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => task);
        var closed = f.Observer.Closed.Single().Result;
        closed.IsCancelled.ShouldBeTrue();
        closed.Exception.ShouldBeAssignableTo<OperationCanceledException>();
    }


    [Fact]
    public async Task PresenterFault_IsReportedWithTheException()
    {
        var f = Build();
        f.Presenter.Fault = new InvalidOperationException("no window");

        await Should.ThrowAsync<InvalidOperationException>(() => f.Navigator.ShowDialog<PickViewModel, string>());

        f.Observer.Closed.Single().Result.Exception!.Message.ShouldBe("no window");
    }


    [Fact]
    public async Task NoObservers_NothingIsTracked()
    {
        var f = Build(observe: false);
        var task = f.Navigator.ShowDialog<PickViewModel, string>();

        f.Dialogs.HasObservers.ShouldBeFalse();
        f.Dialogs.Open.ShouldBeEmpty();

        f.Presenter.UserDismiss();
        (await task).IsCancelled.ShouldBeTrue();
    }


    [Fact]
    public async Task ThrowingObserver_DoesNotBreakTheDialog()
    {
        var f = Build();
        f.Observer.OnShown = dialog =>
        {
            dialog.TryComplete("ok");
            throw new InvalidOperationException("observer");
        };

        (await f.Navigator.ShowDialog<PickViewModel, string>()).Value.ShouldBe("ok");
    }
}
