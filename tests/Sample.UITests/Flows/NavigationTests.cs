using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>The Push &amp; Pop page: route and ViewModel navigation, arguments both ways, pop to root, confirmation.</summary>
public sealed class NavigationTests(AppFixture app) : UiTest(app)
{
    const string Page = "NavigationDemoPage";
    const string Detail = "DetailPage";

    public override async ValueTask InitializeAsync() => await new HomeList(this.Ui).Open("Push & Pop");

    [Theory]
    [InlineData("Push by Route")]
    [InlineData("Push by ViewModel")]
    public async Task Push_passes_the_argument_as_a_query_parameter(string button)
    {
        await this.Ui.Fill(Page, "UITest arg");
        await this.Ui.TapButton(button);

        await this.Ui.WaitForPage(Detail);
        await this.Ui.WaitForLabel(Detail, "Text: UITest arg");
        (await this.Ui.Labels(Detail)).ShouldContain("Source: query parameter");
    }

    [Fact]
    public async Task Push_by_ViewModel_configure_sets_the_property_on_the_ViewModel()
    {
        await this.Ui.Fill(Page, "Configured");
        await this.Ui.TapButton("Push by ViewModel (configure)");

        await this.Ui.WaitForPage(Detail);
        await this.Ui.WaitForLabel(Detail, "Text: Configured (configured)");
        (await this.Ui.Labels(Detail)).ShouldNotContain("Source: query parameter");
    }

    [Fact]
    public async Task Go_back_with_an_argument_returns_it_to_the_previous_page()
    {
        await this.Ui.TapButton("Push by Route");
        await this.Ui.WaitForPage(Detail);

        await this.Ui.TapButton("Go Back with Arg");

        await this.Ui.WaitForPageGone(Detail);
        await this.Ui.WaitForLabel(Page, "Back result: Returned from Detail");
    }

    [Fact]
    public async Task OnNavigatingFrom_can_add_an_argument_to_a_plain_go_back()
    {
        await this.Ui.TapButton("Push by Route");
        await this.Ui.WaitForPage(Detail);

        // Detail's Go Back passes nothing - its OnNavigatingFrom fills BackArg in
        await this.Ui.TapButton("Go Back");

        await this.Ui.WaitForPageGone(Detail);
        await this.Ui.WaitForLabel(Page, "Back result: Set by OnNavigatingFrom");
    }

    [Fact]
    public async Task Pop_to_root_clears_the_whole_stack()
    {
        await this.Ui.TapButton("Push by Route");
        await this.Ui.WaitForPage(Detail);
        await this.Ui.TapButton("Push Another Detail");
        await this.Ui.WaitUntil(async () => (await this.Ui.Query(type: Detail)).Count == 2, "a second Detail page");
        await this.Ui.WaitForLabel(Detail, "Text: Pushed from Detail");

        // Both Detail pages carry the button - the newest one is on top
        await this.Ui.TapButton("Pop to Root");

        await this.Ui.WaitForPageGone(Detail);
        await this.Ui.WaitForPageGone(Page);
        (await this.Ui.Route()).ShouldBe("//HomePage");
    }

    const string LeaveQuestion = "Are you sure you want to navigate away?";

    async Task PushDetailRequiringConfirmation()
    {
        await this.Ui.TapButton("Push by Route");
        await this.Ui.WaitForPage(Detail);

        var toggle = (await this.Ui.Query(selector: $"{Detail} Switch")).Single();
        await this.Ui.SetProperty(toggle.Id, "IsToggled", "true");
        await this.Ui.WaitForLabel(Detail, "Enabled: True");
    }

    // INavigationConfirmation guards user-driven navigation only (tab, flyout, back button). The
    // back button is native - DevFlow cannot touch it - and a Shell-driven `ui navigate ..` blocks
    // the agent while the confirm dialog waits, so the guarded half is not covered here.
    [Fact]
    public async Task Navigation_confirmation_is_not_asked_for_the_apps_own_GoBack()
    {
        await this.PushDetailRequiringConfirmation();

        // INavigator.GoBack is the app's decision, not the user's - documented as unguarded
        await this.Ui.TapButton("Go Back");

        await this.Ui.WaitForPageGone(Detail);
        (await new Dialogs(this.Ui).IsShowing(LeaveQuestion)).ShouldBeFalse();
    }
}
