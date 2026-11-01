using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>INavigator.CreateBuilder() - several segments in one navigation.</summary>
public sealed class BuilderTests(AppFixture app) : UiTest(app)
{
    const string Page = "BuilderDemoPage";
    const string Detail = "DetailPage";

    public override async ValueTask InitializeAsync() => await new HomeList(this.Ui).Open("Builder Chain");

    [Fact]
    public async Task A_chain_pushes_every_segment_in_order()
    {
        await this.Ui.TapButton("Push 3 Details in a Chain");

        await this.Ui.WaitUntil(async () => (await this.Ui.Query(type: Detail)).Count == 3, "three Detail pages");
        await this.Ui.WaitForLabel(Detail, "Text: Chain Step 1");

        // Going back unwinds the chain one segment at a time
        await this.Ui.TapButton("Go Back");
        await this.Ui.WaitUntil(async () => (await this.Ui.Query(type: Detail)).Count == 2, "two Detail pages");
        await this.Ui.TapButton("Go Back");
        await this.Ui.WaitUntil(async () => (await this.Ui.Query(type: Detail)).Count == 1, "one Detail page");
        await this.Ui.TapButton("Go Back");
        await this.Ui.WaitForPageGone(Detail);
        (await this.Ui.Exists(Page)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_chain_can_end_in_a_modal()
    {
        await this.Ui.TapButton("Push Detail then Modal target");

        await this.Ui.WaitForPage("ModalDemoPage");
        await this.Ui.WaitForLabel("ModalDemoPage", "Title: Chained Modal");
        await this.Ui.WaitForLabel(Detail, "Text: Before Modal");

        await this.Ui.TapButton("Close Modal");
        await this.Ui.WaitForPageGone("ModalDemoPage");
        (await this.Ui.Exists(Detail)).ShouldBeTrue("closing the modal lands on the Detail pushed before it");
    }

    [Fact]
    public async Task Pop_then_push_replaces_the_current_page()
    {
        await this.Ui.TapButton("Pop 1 and Push Detail");

        await this.Ui.WaitForPage(Detail);
        await this.Ui.WaitForLabel(Detail, "Text: Replaced via Pop+Push");
        await this.Ui.WaitForPageGone(Page);

        await this.Ui.TapButton("Go Back");
        await this.Ui.WaitForPageGone(Detail);
        (await this.Ui.Route()).ShouldBe("//HomePage");
    }
}
