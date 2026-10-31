namespace Shiny;


/// <summary>
/// A dialog ViewModel on screen, shown with <see cref="INavigator.ShowDialog{TViewModel, T}"/>.
/// Handed to <see cref="IDialogObserver"/>s, and able to close the dialog itself - which is how a
/// test or an automation tool answers a dialog without tapping it.
/// </summary>
public sealed class PresentedDialog
{
    readonly Func<object?, bool> complete;
    readonly Func<bool> cancel;
    int closed;

    internal PresentedDialog(
        long id,
        object viewModel,
        Type resultType,
        Page page,
        Func<object?, bool> complete,
        Func<bool> cancel
    )
    {
        this.Id = id;
        this.ViewModel = viewModel;
        this.ResultType = resultType;
        this.Page = page;
        this.ShownAt = DateTimeOffset.Now;
        this.complete = complete;
        this.cancel = cancel;
    }


    /// <summary>Unique for the life of the app.</summary>
    public long Id { get; }

    /// <summary>The dialog ViewModel - an <see cref="IDialogAware{T}"/>.</summary>
    public object ViewModel { get; }

    /// <summary>The ViewModel's type.</summary>
    public Type ViewModelType => this.ViewModel.GetType();

    /// <summary>The <c>T</c> of <see cref="IDialogAware{T}"/> - what <see cref="TryComplete"/> accepts.</summary>
    public Type ResultType { get; }

    /// <summary>The page the presenter is showing.</summary>
    public Page Page { get; }

    /// <summary>When the dialog was presented.</summary>
    public DateTimeOffset ShownAt { get; }

    /// <summary>False once the dialog has closed and the observers have been told.</summary>
    public bool IsOpen => Volatile.Read(ref this.closed) == 0;


    /// <summary>
    /// Closes the dialog with <paramref name="value"/>, exactly as if the ViewModel had raised
    /// <see cref="IDialogAware{T}.Completed"/>. The awaiting <c>ShowDialog</c> caller receives it.
    /// </summary>
    /// <returns>False when the dialog already had a result - the first result wins.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a <see cref="ResultType"/>.</exception>
    public bool TryComplete(object? value) => this.complete(value);


    /// <summary>
    /// Closes the dialog as cancelled, exactly as if the ViewModel had raised
    /// <see cref="IDialogAware{T}.Cancelled"/>.
    /// </summary>
    /// <returns>False when the dialog already had a result.</returns>
    public bool TryCancel() => this.cancel();


    internal void MarkClosed() => Volatile.Write(ref this.closed, 1);
}


/// <summary>
/// How a <see cref="PresentedDialog"/> closed.
/// </summary>
/// <param name="IsCancelled">True when the ViewModel or the user cancelled, the dialog was dismissed, or it failed.</param>
/// <param name="Value">The value returned to the caller; null when cancelled.</param>
/// <param name="Exception">
/// Set when the dialog did not close normally: the presenter faulted, or the caller's
/// <see cref="CancellationToken"/> fired (an <see cref="OperationCanceledException"/>).
/// </param>
/// <param name="Duration">How long the dialog was open.</param>
public sealed record DialogClosed(
    bool IsCancelled,
    object? Value,
    Exception? Exception,
    TimeSpan Duration
);


/// <summary>
/// Watches dialog ViewModels shown with <see cref="INavigator.ShowDialog{TViewModel, T}"/> - when
/// each is presented and how it closed. Add one to the service collection, or with
/// <see cref="ShinyAppBuilder.AddDialogObserver{TObserver}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only <c>ShowDialog</c> dialogs are reported - they are the ones Shiny presents. Alerts, confirms,
/// prompts and action sheets from <see cref="IDialogs"/> belong to whichever implementation is
/// registered; decorate <see cref="IDialogs"/> to observe those.
/// </para>
/// <para>
/// Callbacks run synchronously and an exception thrown by an observer is logged and ignored. Hold
/// on to the <see cref="PresentedDialog"/> to close the dialog later with
/// <see cref="PresentedDialog.TryComplete"/> or <see cref="PresentedDialog.TryCancel"/>.
/// </para>
/// </remarks>
public interface IDialogObserver
{
    /// <summary>A dialog has been handed to the <see cref="IDialogPresenter"/>.</summary>
    void OnDialogShown(PresentedDialog dialog) { }

    /// <summary>A dialog has closed and its result is about to be returned to the caller.</summary>
    void OnDialogClosed(PresentedDialog dialog, DialogClosed result) { }
}
