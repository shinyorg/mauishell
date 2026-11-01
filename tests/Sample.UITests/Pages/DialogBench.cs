using Sample.UITests.DevFlow;

namespace Sample.UITests.Pages;

/// <summary>The Sample's DialogSwitch.Provider values, in Picker order.</summary>
public enum DialogProviderKind
{
    Shell,
    ShinyControls,
    UxDivers
}

/// <summary>The Sample's DialogSwitch.Presenter values, in Picker order.</summary>
public enum DialogPresenterKind
{
    ShellModal,
    ShinyOverlay,
    UxDiversPopup
}

/// <summary>
/// DialogDemoPage. Its two Pickers choose the IDialogs provider and IDialogPresenter the whole
/// Sample uses (persisted in Preferences).
/// <para>
/// Only <see cref="DialogProviderKind.ShinyControls"/> is driveable: the Shell provider's native
/// alerts are invisible to DevFlow's <c>ui alert</c> on the iOS 27 simulator, and UXDivers popups
/// are not in the MAUI tree. Presenters: ShellModal and ShinyOverlay both put the dialog's page
/// content in the tree; UxDiversPopup does not.
/// </para>
/// </summary>
public sealed class DialogBench(NativeUi ui)
{
    public const string Page = "DialogDemoPage";

    /// <summary>Opens the bench from the home list if needed and selects a provider and presenter.</summary>
    public async Task Use(DialogProviderKind provider, DialogPresenterKind presenter)
    {
        if (!await ui.Exists(Page))
            await new HomeList(ui).Open("Dialog Bench");

        var pickers = await ui.Query(selector: $"{Page} Picker");
        pickers.Count.ShouldBe(2, "the bench has a provider picker and a presenter picker");

        await ui.SetProperty(pickers[0].Id, "SelectedIndex", ((int)provider).ToString());
        await ui.SetProperty(pickers[1].Id, "SelectedIndex", ((int)presenter).ToString());

        (await ui.Property(pickers[0].Id, "SelectedItem")).ShouldBe(provider.ToString());
        (await ui.Property(pickers[1].Id, "SelectedItem")).ShouldBe(presenter.ToString());
    }

    /// <summary>
    /// The "Last Result" label: what the command expected and what it got, e.g.
    /// "Confirm\nexpected: ...\nactual: True". Returns the text after "actual: ".
    /// </summary>
    public async Task<string> WaitForResult(string what, TimeSpan? timeout = null)
    {
        var text = await ui.WaitForLabelStartingWith(Page, what + "\n", timeout);
        var marker = text.IndexOf("actual: ", StringComparison.Ordinal);
        return marker < 0 ? text : text[(marker + "actual: ".Length)..];
    }

    /// <summary>The on-screen event log, newest first, without timestamps.</summary>
    public async Task<IReadOnlyList<string>> Log()
        => (await ui.Labels(Page))
            .Where(l => l.Length > 14 && l[2] == ':' && l[5] == ':' && l[8] == '.')
            .Select(l => l[14..].Trim())
            .ToList();
}
