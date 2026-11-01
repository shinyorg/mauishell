using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>A Shell.PresentationMode="Modal" page: navigation inside it, closing it, and dialogs raised over it.</summary>
public sealed class ModalTests(AppFixture app) : UiTest(app)
{
    const string Modal = "ModalDemoPage";

    [Fact]
    public async Task Opens_with_its_default_title_and_closes()
    {
        await new HomeList(this.Ui).Open("Modal");
        await this.Ui.WaitForLabel(Modal, "Title: Modal Page");
        (await this.Ui.Labels(Modal)).ShouldContain("Note: (none)");

        await this.Ui.TapButton("Close Modal");

        await this.Ui.WaitForPageGone(Modal);
        (await this.Ui.Exists("HomePage")).ShouldBeTrue();
    }

    [Fact]
    public async Task Pushes_a_page_inside_the_modal_and_comes_back_to_it()
    {
        await new HomeList(this.Ui).Open("Modal");

        await this.Ui.TapButton("Push Detail Within Modal");
        await this.Ui.WaitForPage("DetailPage");
        await this.Ui.WaitForLabel("DetailPage", "Text: Inside Modal");

        await this.Ui.TapButton("Go Back");
        await this.Ui.WaitForPageGone("DetailPage");
        (await this.Ui.Exists(Modal)).ShouldBeTrue("going back inside a modal stays in the modal");
    }

    [Theory]
    [InlineData(DialogPresenterKind.ShellModal)]
    [InlineData(DialogPresenterKind.ShinyOverlay)]
    public async Task A_dialog_raised_from_a_modal_returns_its_result(DialogPresenterKind presenter)
    {
        var bench = new DialogBench(this.Ui);
        await bench.Use(DialogProviderKind.ShinyControls, presenter);

        // The bench's own way in: the modal with its required Title and optional note set
        await this.Ui.TapButton("Dialog over a modal page");
        await this.Ui.WaitForPage(Modal);
        await this.Ui.WaitForLabel(Modal, "Title: Dialog over a modal");
        (await this.Ui.Labels(Modal)).ShouldContain("Note: Show the dialog from here - it should land on top of this page");

        await this.Ui.TapButton("Show dialog from this modal");
        await this.Ui.WaitForLabelStartingWith(presenter == DialogPresenterKind.ShellModal ? "PickColorPage" : Modal, "Preset: Blue");
        await this.Ui.TapButton("Green");

        await this.Ui.WaitForLabel(Modal, "Dialog: Green");
    }
}
