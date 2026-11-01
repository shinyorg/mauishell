using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>
/// The dialog bench: the four IDialogs primitives and ViewModel dialogs (IDialogAware through the
/// generated Show*Dialog extensions), under both presenters DevFlow can see.
/// </summary>
public sealed class DialogTests(AppFixture app) : UiTest(app)
{
    DialogBench Bench => new(this.Ui);
    Dialogs Dialogs => new(this.Ui);

    public override async ValueTask InitializeAsync()
        => await this.Bench.Use(DialogProviderKind.ShinyControls, DialogPresenterKind.ShellModal);

    // --- IDialogs ---------------------------------------------------------------------------------

    [Fact]
    public async Task Alert_returns_once_dismissed()
    {
        await this.Ui.TapButton("Alert");
        await this.Dialogs.Answer("This is a simple alert dialog.", "OK");

        (await this.Bench.WaitForResult("Alert")).ShouldBe("dismissed");
    }

    [Theory]
    [InlineData("Yes", "True")]
    [InlineData("No", "False")]
    public async Task Confirm_returns_the_answer(string button, string expected)
    {
        await this.Ui.TapButton("Confirm");
        await this.Dialogs.Answer("Do you agree?", button);

        (await this.Bench.WaitForResult("Confirm")).ShouldBe(expected);
    }

    [Fact]
    public async Task Prompt_returns_the_typed_text()
    {
        await this.Ui.TapButton("Prompt");
        await this.Dialogs.WaitFor("What is your name?");

        var entry = (await this.Ui.Query(type: "Entry")).Last(e => e.IsVisible);
        await this.Ui.Send($"ui fill {entry.Id} \"Ada Lovelace\"");
        await this.Ui.TapButton("OK");

        (await this.Bench.WaitForResult("Prompt")).ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task Prompt_returns_null_when_cancelled()
    {
        await this.Ui.TapButton("Prompt");
        await this.Dialogs.Answer("What is your name?", "Cancel");

        (await this.Bench.WaitForResult("Prompt")).ShouldBe("(null)");
    }

    [Fact]
    public async Task ActionSheet_returns_the_button_tapped()
    {
        await this.Ui.TapButton("Action Sheet");
        await this.Ui.TapButton("Share");

        (await this.Bench.WaitForResult("ActionSheet")).ShouldBe("Share");
    }

    // --- ViewModel dialogs (IDialogPresenter) ----------------------------------------------------

    public static TheoryData<DialogPresenterKind> Presenters => new(DialogPresenterKind.ShellModal, DialogPresenterKind.ShinyOverlay);

    [Theory]
    [MemberData(nameof(Presenters))]
    public async Task A_ViewModel_dialog_returns_the_typed_result(DialogPresenterKind presenter)
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, presenter);

        await this.Ui.TapButton("ViewModel dialog (awaits a typed result)");
        await this.WaitForColourPicker(presenter, "Violet");
        await this.Ui.TapButton("Green");

        (await this.Bench.WaitForResult("ViewModel dialog")).ShouldBe("'Green'");
    }

    [Theory]
    [MemberData(nameof(Presenters))]
    public async Task A_ViewModel_dialog_reports_cancel_as_IsCancelled(DialogPresenterKind presenter)
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, presenter);

        await this.Ui.TapButton("ViewModel dialog (awaits a typed result)");
        await this.WaitForColourPicker(presenter, "Violet");
        await this.Ui.TapButton("Cancel");

        (await this.Bench.WaitForResult("ViewModel dialog")).ShouldBe("IsCancelled");
    }

    [Theory]
    [MemberData(nameof(Presenters))]
    public async Task A_ViewModel_dialog_gets_its_lifecycle_and_is_disposed(DialogPresenterKind presenter)
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, presenter);
        await this.Ui.TapButton("Clear");

        await this.Ui.TapButton("ViewModel dialog (awaits a typed result)");
        await this.WaitForColourPicker(presenter, "Violet");
        await this.Ui.TapButton("Red");
        await this.Bench.WaitForResult("ViewModel dialog");

        await this.Ui.WaitUntil(
            async () => (await this.Bench.Log()).Contains("PickColorViewModel.Dispose"),
            "the dialog's ViewModel to be disposed"
        );
        var log = await this.Bench.Log();
        log.ShouldContain("PickColorViewModel.OnAppearing - preset Violet");
    }

    [Theory]
    [MemberData(nameof(Presenters))]
    public async Task A_dialog_can_open_a_nested_dialog(DialogPresenterKind presenter)
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, presenter);

        await this.Ui.TapButton("ViewModel dialog (awaits a typed result)");
        await this.WaitForColourPicker(presenter, "Violet");

        await this.Ui.TapButton("Nested dialog");
        await this.Ui.TapButton("Accept");
        await this.Ui.WaitUntil(
            async () => (await this.Ui.Query(type: "Label")).Any(l => l.Text == "Nested: accepted"),
            "the nested result on the outer dialog"
        );

        await this.Ui.TapButton("Blue");
        (await this.Bench.WaitForResult("ViewModel dialog")).ShouldBe("'Blue'");
    }

    [Theory]
    [MemberData(nameof(Presenters))]
    public async Task A_callers_cancellation_token_tears_the_dialog_down(DialogPresenterKind presenter)
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, presenter);

        await this.Ui.TapButton("Cancelled by the caller after 3s");
        await this.WaitForColourPicker(presenter, "Red");

        // Nobody answers; the caller's 3s token fires
        (await this.Bench.WaitForResult("Caller-cancelled dialog", TimeSpan.FromSeconds(15)))
            .ShouldBe("OperationCanceledException, dialog torn down");
        await this.Ui.WaitUntil(
            async () => !(await this.Ui.Query(type: "Label")).Any(l => l.Text == "Preset: Red"),
            "the dialog to be removed"
        );
    }

    [Fact]
    public async Task Navigating_away_under_an_overlay_dialog_cancels_it_rather_than_hanging()
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, DialogPresenterKind.ShinyOverlay);
        await this.Ui.TapButton("Clear");

        // The ViewModel goes back after 2s while the dialog is still up. The overlay lives inside the
        // bench page, so the page leaving takes it with it.
        await this.Ui.TapButton("Dialog, then navigate away after 2s");
        await this.WaitForColourPicker(DialogPresenterKind.ShinyOverlay, "Green");
        await this.Ui.WaitForPageGone(DialogBench.Page, TimeSpan.FromSeconds(15));

        // The awaiting command logs to the app-wide event log once its await completes
        await new HomeList(this.Ui).Open("Dialog Bench");
        await this.Ui.WaitUntil(
            async () => (await this.Bench.Log()).Contains("Dialog then navigate away -> IsCancelled"),
            "the abandoned dialog's await to complete as cancelled",
            TimeSpan.FromSeconds(15)
        );
    }

    [Fact]
    public async Task GoBack_under_a_modal_dialog_closes_the_dialog_as_cancelled()
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, DialogPresenterKind.ShellModal);
        await this.Ui.TapButton("Clear");

        // The ShellModal presenter puts the dialog on the navigation stack, so a GoBack 2s in pops
        // the dialog, not the page under it
        await this.Ui.TapButton("Dialog, then navigate away after 2s");
        await this.WaitForColourPicker(DialogPresenterKind.ShellModal, "Green");

        await this.Ui.WaitForPageGone("PickColorPage", TimeSpan.FromSeconds(15));
        (await this.Bench.WaitForResult("Dialog then navigate away")).ShouldBe("IsCancelled");
        (await this.Ui.Exists(DialogBench.Page)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_ShinyContentPage_hosts_the_overlay_dialog_in_its_OverlayHost()
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, DialogPresenterKind.ShinyOverlay);
        await this.Ui.TapButton("Dialog from a ShinyContentPage");
        await this.Ui.WaitForPage("OverlayHostDemoPage");

        await this.Ui.TapButton("Show dialog");
        await this.Ui.WaitForLabel("OverlayHostDemoPage", "Preset: Blue");
        await this.Ui.TapButton("Violet");

        await this.Ui.WaitForLabel("OverlayHostDemoPage", "Violet");
    }

    [Fact]
    public async Task A_ShinyContentPage_dialog_layers_above_its_loading_overlay()
    {
        await this.Bench.Use(DialogProviderKind.ShinyControls, DialogPresenterKind.ShinyOverlay);
        await this.Ui.TapButton("Dialog from a ShinyContentPage");
        await this.Ui.WaitForPage("OverlayHostDemoPage");

        await this.Ui.TapButton("Show dialog over the loading overlay");
        await this.Ui.WaitForLabel("OverlayHostDemoPage", "Preset: Green");
        await this.Ui.TapButton("Red");

        await this.Ui.WaitForLabel("OverlayHostDemoPage", "Red (over loading overlay)");
    }

    /// <summary>
    /// ShellModal presents PickColorPage itself; ShinyOverlay puts its content into the bench page.
    /// Either way the preset label shows once the dialog is up.
    /// </summary>
    async Task WaitForColourPicker(DialogPresenterKind presenter, string preset)
    {
        var host = presenter == DialogPresenterKind.ShellModal ? "PickColorPage" : DialogBench.Page;
        await this.Ui.WaitForLabel(host, $"Preset: {preset}");
    }
}
