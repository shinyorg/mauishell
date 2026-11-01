using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>The Navigate.* attached properties - navigation with no ViewModel command.</summary>
public sealed class XamlNavigationTests(AppFixture app) : UiTest(app)
{
    const string Page = "XamlNavDemoPage";
    const string Detail = "DetailPage";

    public override async ValueTask InitializeAsync() => await new HomeList(this.Ui).Open("XAML Declarative");

    [Fact]
    public async Task A_bound_parameter_value_is_passed()
    {
        await this.Ui.Fill(Page, "Typed in XAML demo");
        await this.Ui.TapButton("Navigate to Detail");

        await this.Ui.WaitForPage(Detail);
        await this.Ui.WaitForLabel(Detail, "Text: Typed in XAML demo");
    }

    [Fact]
    public async Task A_route_without_parameters_navigates()
    {
        await this.Ui.TapButton("Navigate to Detail (no params)");

        await this.Ui.WaitForPage(Detail);
        (await this.Ui.Labels(Detail)).ShouldContain("Text: ");
        (await this.Ui.Exists(Page)).ShouldBeTrue("relative navigation pushes on top");
    }

    [Fact]
    public async Task RelativeNavigation_false_replaces_the_stack()
    {
        // Route "HomePage/DetailPage" with RelativeNavigation=False -> //HomePage/DetailPage
        await this.Ui.TapButton("Set Root to Detail");

        await this.Ui.WaitForPage(Detail);
        await this.Ui.WaitForLabel(Detail, "Text: Absolute nav");
        await this.Ui.WaitForPageGone(Page);
        (await this.Ui.Route()).ShouldBe("//HomePage/DetailPage");
    }
}
