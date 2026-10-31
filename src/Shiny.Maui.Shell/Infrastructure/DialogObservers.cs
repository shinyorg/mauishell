using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


/// <summary>
/// Reports <c>ShowDialog</c> dialogs to the registered <see cref="IDialogObserver"/>s and keeps
/// the list of dialogs currently open. A no-op when no observer is registered.
/// </summary>
public class DialogObservers(
    ILogger<DialogObservers> logger,
    IServiceProvider services
)
{
    long lastId;
    IReadOnlyList<IDialogObserver>? observers;
    readonly ConcurrentDictionary<long, (PresentedDialog Dialog, long Timestamp)> open = new();


    IReadOnlyList<IDialogObserver> Observers
        => this.observers ??= services.GetServices<IDialogObserver>().ToList();


    /// <summary>Whether anything is observing.</summary>
    public bool HasObservers => this.Observers.Count > 0;


    /// <summary>Dialogs presented and not yet closed, oldest first.</summary>
    public IReadOnlyList<PresentedDialog> Open
        => this.open.Values.OrderBy(x => x.Dialog.Id).Select(x => x.Dialog).ToList();


    /// <summary>
    /// Creates the handle for a dialog about to be presented, wired to the completion the
    /// navigator awaits. Returns null when nobody is observing.
    /// </summary>
    public PresentedDialog? Create<T>(object viewModel, Page page, TaskCompletionSource<DialogResult<T>> completion)
    {
        if (!this.HasObservers)
            return null;

        return new PresentedDialog(
            Interlocked.Increment(ref this.lastId),
            viewModel,
            typeof(T),
            page,
            value =>
            {
                // A null is a valid T only when T can hold one.
                if (value is T typed)
                    return completion.TrySetResult(DialogResult<T>.Complete(typed));

                if (value == null && default(T) == null)
                    return completion.TrySetResult(DialogResult<T>.Complete(default!));

                throw new ArgumentException(
                    $"Dialog '{viewModel.GetType().Name}' returns '{typeof(T)}', which '{value?.GetType().ToString() ?? "null"}' is not",
                    nameof(value)
                );
            },
            () => completion.TrySetResult(DialogResult<T>.Cancel())
        );
    }


    /// <summary>Reports a dialog presented.</summary>
    public void Shown(PresentedDialog? dialog)
    {
        if (dialog == null)
            return;

        this.open[dialog.Id] = (dialog, Stopwatch.GetTimestamp());
        foreach (var observer in this.Observers)
        {
            try
            {
                observer.OnDialogShown(dialog);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Observer] '{observer}' threw from OnDialogShown", observer.GetType().Name);
            }
        }
    }


    /// <summary>Reports a dialog closed. Only the first call for a dialog reports anything.</summary>
    public void Closed(PresentedDialog? dialog, bool isCancelled, object? value, Exception? exception = null)
    {
        if (dialog == null || !this.open.TryRemove(dialog.Id, out var entry))
            return;

        dialog.MarkClosed();
        var result = new DialogClosed(
            isCancelled,
            isCancelled ? null : value,
            exception,
            Stopwatch.GetElapsedTime(entry.Timestamp)
        );

        foreach (var observer in this.Observers)
        {
            try
            {
                observer.OnDialogClosed(dialog, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Observer] '{observer}' threw from OnDialogClosed", observer.GetType().Name);
            }
        }
    }
}
