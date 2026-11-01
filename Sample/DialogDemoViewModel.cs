using Shiny;

namespace Sample;

/// <summary>
/// The dialog bench - every <c>IDialogs</c> provider and every <c>IDialogPresenter</c>, switchable at
/// runtime, plus the paths where presenters actually break rather than the happy one.
/// </summary>
/// <remarks>
/// Each command records what it expects alongside what happened, so a manual run reads as pass/fail
/// instead of "hmm, that looked about right".
/// </remarks>
[ShellMap<DialogDemoPage>(
    Shortcut = "Dialog Bench",
    ShortcutSubtitle = "Every provider, switchable",
    ShortcutOrder = 1
)]
public partial class DialogDemoViewModel(
    IDialogs dialogs,
    INavigator navigator,
    DialogSwitch dialogSwitch,
    DialogEventLog log
) : ObservableObject
{
    public DialogSwitch Switch => dialogSwitch;
    public DialogEventLog Log => log;

    public IReadOnlyList<DialogProvider> Providers { get; } = Enum.GetValues<DialogProvider>();
    public IReadOnlyList<DialogPresenterKind> Presenters { get; } = Enum.GetValues<DialogPresenterKind>();

    [ObservableProperty] string lastResult = "(none)";

    void Record(string what, string expected, string actual)
    {
        this.LastResult = $"{what}\nexpected: {expected}\nactual: {actual}";
        log.Add($"{what} -> {actual}");
    }


    // --- IDialogs: the four primitives, against the selected provider -------------------------

    [RelayCommand]
    async Task ShowAlert()
    {
        await dialogs.Alert("Alert", "This is a simple alert dialog.");
        this.Record("Alert", "returns after the button or an outside tap", "dismissed");
    }


    [RelayCommand]
    async Task ShowConfirm()
    {
        var result = await dialogs.Confirm("Confirm", "Do you agree?");
        this.Record("Confirm", "true only when you tapped Yes", result.ToString());
    }


    [RelayCommand]
    async Task ShowPrompt()
    {
        var result = await dialogs.Prompt("Prompt", "What is your name?", placeholder: "Name");
        this.Record("Prompt", "your text, or null when cancelled", result ?? "(null)");
    }


    [RelayCommand]
    async Task ShowActionSheet()
    {
        var result = await dialogs.ActionSheet("Pick an Option", "Cancel", "Delete", "Edit", "Share", "Copy");
        this.Record("ActionSheet", "the button you tapped", result);
    }


    // --- IDialogPresenter: ViewModel dialogs, against the selected presenter -------------------

    // ShowPickColorDialog is source generated from [ShellMap<PickColorPage>] + IDialogAware<string>
    // on PickColorViewModel - no type arguments, and [ShellProperty] values become parameters
    [RelayCommand]
    async Task ShowViewModelDialog()
    {
        var result = await navigator.ShowPickColorDialog(preset: "Violet");
        this.Record(
            "ViewModel dialog",
            "the colour you picked; IsCancelled for Cancel, a backdrop tap or hardware back",
            Describe(result)
        );
    }


    [RelayCommand]
    async Task ShowTallDialog()
    {
        var result = await navigator.ShowLongTextDialog();
        this.Record(
            "Tall dialog",
            "card stays on screen and scrolls inside itself - it must not run off the edges",
            Describe(result)
        );
    }


    [RelayCommand]
    async Task ShowAutoCancelledDialog()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            var result = await navigator.ShowPickColorDialog(preset: "Red", cancellationToken: cancel.Token);
            this.Record(
                "Caller-cancelled dialog",
                "OperationCanceledException after 3s - a caller's token is not a cancelled result",
                $"returned {Describe(result)} (you beat the timer)"
            );
        }
        catch (OperationCanceledException)
        {
            this.Record(
                "Caller-cancelled dialog",
                "OperationCanceledException after 3s - a caller's token is not a cancelled result",
                "OperationCanceledException, dialog torn down"
            );
        }
    }


    [RelayCommand]
    async Task ShowThenNavigateAway()
    {
        // Navigating away 2s in is the case that hangs if a presenter has no answer for its host
        // disappearing - the overlay lives inside this page, so the page leaving takes it with it.
        _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(
            _ => navigator.GoBack(),
            TaskScheduler.Default
        );

        var result = await navigator.ShowPickColorDialog(preset: "Green");
        this.Record(
            "Dialog then navigate away",
            "the await completes (cancelled for the overlay presenter) rather than hanging - check the log after coming back",
            Describe(result)
        );
    }


    // Typed, not a route with args: ModalDemoViewModel takes its values through [ShellProperty],
    // and string-based args only reach a ViewModel that implements IQueryAttributable
    [RelayCommand]
    Task ShowDialogOverModal() => navigator.NavigateTo<ModalDemoViewModel>(x =>
    {
        x.Title = "Dialog over a modal";
        x.OptionalNote = "Show the dialog from here - it should land on top of this page";
    });


    [RelayCommand]
    Task OpenOverlayHostPage() => navigator.NavigateTo(nameof(OverlayHostDemoPage));


    [RelayCommand]
    void ClearLog() => log.Clear();


    static string Describe(DialogResult<string> result)
        => result.IsCancelled ? "IsCancelled" : $"'{result.Value}'";
}
