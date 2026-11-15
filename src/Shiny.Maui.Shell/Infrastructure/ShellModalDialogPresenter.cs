namespace Shiny.Infrastructure;

/// <summary>
/// The default <see cref="IDialogPresenter"/> - pushes the dialog page onto the Window's modal stack.
/// </summary>
/// <remarks>
/// Two deliberate choices here, both verified against MAUI's <c>ModalNavigationManager</c>:
///
/// <para><b>The Window's navigation, not the Shell's.</b> <c>Shell.Navigation</c> is a
/// <c>NavigationProxy</c> that reinterprets modal calls: outside an active Shell navigation its
/// <c>OnPopModal</c> turns into <c>Shell.GoToAsync("..")</c>, which is a route navigation - it would
/// run our <c>INavigationConfirmation</c> guard, raise Shell's navigating events, and pop whatever
/// Shell believes is current rather than this page. <c>Window.Navigation</c> goes straight to
/// <c>ModalNavigationManager</c>, which is the behaviour a dialog wants.</para>
///
/// <para><b>Dismissal is detected via <see cref="Element.ParentChanged"/>, not
/// <c>Window.ModalPopped</c>.</b> Every teardown path in <c>ModalNavigationManager</c> ends in
/// <c>RemoveLogicalChild(page)</c> - both the ordinary <c>PopModalAsync</c> and
/// <c>SyncPlatformModalStackAsync</c>, which reconciles a platform-initiated dismissal. Only the
/// former raises <c>ModalPopped</c>, so watching the parent catches strictly more cases.</para>
///
/// Pushing modally rather than navigating to the page's route (with
/// <c>Shell.PresentationMode="Modal"</c> declared in XAML) presents modally regardless of what the
/// page's XAML says, and hands the navigator the exact page instance so there is no
/// <see cref="ShellNavigationConfigurator"/> pinning race.
/// </remarks>
public class ShellModalDialogPresenter : IDialogPresenter
{
    public async Task Present(Page page, object viewModel, CancellationToken dismiss)
    {
        var shell = Shell.Current
            ?? throw new InvalidOperationException("There is no active Shell to present a dialog on");

        var window = shell.Window
            ?? throw new InvalidOperationException("The active Shell has no Window to present a dialog on");

        var navigation = window.Navigation;

        // Completed once the page has been detached from the Window - by our own PopModalAsync
        // below, by the user (back button / swipe-down), or by the platform modal stack sync.
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnParentChanged(object? sender, EventArgs args)
        {
            // Also fires when the push parents the page to the Window - only a cleared parent
            // means the dialog is gone.
            if (page.Parent == null)
                detached.TrySetResult();
        }

        // Subscribed before the push so a synchronous teardown cannot be missed.
        page.ParentChanged += OnParentChanged;
        try
        {
            await MainThread
                .InvokeOnMainThreadAsync(() => navigation.PushModalAsync(page, true))
                .ConfigureAwait(false);

            var dismissRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Fires synchronously here if the token is already cancelled, which is correct - the
            // viewmodel completed before the push finished, so tear straight back down.
            await using var registration = dismiss.Register(() => dismissRequested.TrySetResult());

            await Task.WhenAny(detached.Task, dismissRequested.Task).ConfigureAwait(false);

            if (!detached.Task.IsCompleted)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    // PopModalAsync always pops the top of the stack, so only pop when this page is
                    // the top. The user may also have dismissed it between WhenAny resolving and
                    // this dispatch, in which case it is no longer on the stack at all.
                    var stack = navigation.ModalStack;
                    if (stack.Count > 0 && ReferenceEquals(stack[stack.Count - 1], page))
                        await navigation.PopModalAsync(true).ConfigureAwait(false);
                }).ConfigureAwait(false);

                // PopModalAsync detaches the page, which raises ParentChanged - but complete it
                // ourselves too so the await below can never hang, including the unsupported case
                // where another modal was stacked on top of this dialog.
                detached.TrySetResult();
            }

            await detached.Task.ConfigureAwait(false);
        }
        finally
        {
            page.ParentChanged -= OnParentChanged;
        }
    }
}
