namespace Shiny.Infrastructure;

/// <summary>
/// Base class for <see cref="IDialogPresenter"/> implementations that host a dialog in something
/// that is not a MAUI <see cref="Page"/> - a popup, an in-page overlay, a bottom sheet. It unwraps
/// the dialog page so subclasses only deal with a <see cref="View"/>, and restores the ordering
/// (binding, lifecycle, disposal) that the page would otherwise have given for free.
/// </summary>
/// <remarks>
/// The navigator resolves a <see cref="Page"/> for the dialog ViewModel because that is what the
/// route map holds. A popup host cannot take a page, so the page's <see cref="ContentPage.Content"/>
/// is re-parented into the presentation and handed back afterwards.
///
/// <para>Three things follow from the page never entering the visual tree, and this class handles
/// all of them:</para>
/// <list type="bullet">
/// <item><b>Binding.</b> A <see cref="BindableObject.BindingContext"/> normally flows down from the
/// page; once the content is re-parented its context would be inherited from whatever hosts the
/// popup, so it is set explicitly on the content itself.</item>
/// <item><b>Lifecycle.</b> <see cref="IPageLifecycleAware"/> is driven by
/// <c>Application.PageAppearing</c>/<c>PageDisappearing</c>, which only fire for a real page, so the
/// hooks are raised here instead.</item>
/// <item><b>Disposal.</b> The navigator disposes a dialog ViewModel when its page is removed from
/// the tree; a page that was never in it is never removed, so an <see cref="IDisposable"/> ViewModel
/// is disposed here.</item>
/// </list>
///
/// Unlike the default <see cref="ShellModalDialogPresenter"/>, the page underneath stays on screen
/// and keeps its own lifecycle - it neither disappears when the dialog opens nor reappears when it
/// closes.
/// </remarks>
public abstract class ViewDialogPresenter : IDialogPresenter
{
    public Task Present(Page page, object viewModel, CancellationToken dismiss)
    {
        if (page is not ContentPage contentPage)
            throw new InvalidOperationException(
                $"{this.GetType().Name} presents a page's content, so the dialog page must be a ContentPage - '{page.GetType().FullName}' is not. Use the default ShellModalDialogPresenter for it."
            );

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var content = contentPage.Content
                ?? throw new InvalidOperationException($"Dialog page '{page.GetType().FullName}' has no Content to present");

            // A view can only have one parent - the page has to let go of it before the
            // presentation can take it, which also cuts off the inherited binding context.
            contentPage.Content = null;
            content.BindingContext = viewModel;

            try
            {
                // No ConfigureAwait(false) anywhere below this point, here or in a subclass: the
                // whole presentation runs inside the main thread's synchronization context and every
                // continuation touches the visual tree.
                (viewModel as IPageLifecycleAware)?.OnAppearing();
                await this.PresentView(content, viewModel, dismiss);
            }
            finally
            {
                (viewModel as IPageLifecycleAware)?.OnDisappearing();

                // Only possible once the presentation has released the view. A subclass that leaves
                // it parented keeps it alive with the popup instead - not fatal, and better than
                // throwing over cleanup while the real result is on its way back to the caller.
                if (content.Parent == null)
                    contentPage.Content = content;

                (viewModel as IDisposable)?.Dispose();
            }
        });
    }


    /// <summary>
    /// Presents <paramref name="content"/> and returns a task that completes once it is no longer
    /// shown - either because the user dismissed it or because <paramref name="dismiss"/> fired.
    /// </summary>
    /// <remarks>
    /// Called on the main thread, with <see cref="BindableObject.BindingContext"/> already set on
    /// <paramref name="content"/>. Detach the view from the presentation before returning so it can
    /// be handed back to its page.
    ///
    /// As with <see cref="IDialogPresenter.Present"/>, never throw
    /// <see cref="OperationCanceledException"/> when <paramref name="dismiss"/> fires - teardown by
    /// token is the normal success path.
    /// </remarks>
    /// <param name="content">The dialog page's content, detached from the page.</param>
    /// <param name="viewModel">The dialog ViewModel bound to <paramref name="content"/>.</param>
    /// <param name="dismiss">Signals that the presentation should be torn down.</param>
    protected abstract Task PresentView(View content, object viewModel, CancellationToken dismiss);
}
