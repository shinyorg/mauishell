using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>
/// AskFirstNavigationInterceptor, armed from the Route Interceptor page: an action sheet decides
/// whether a navigation to Detail continues, redirects or is cancelled.
/// </summary>
public sealed class InterceptorTests(AppFixture app) : UiTest(app)
{
    const string Page = "InterceptorDemoPage";
    const string Detail = "DetailPage";

    public override async ValueTask InitializeAsync()
    {
        await new HomeList(this.Ui).Open("Route Interceptor");

        // The guard switch is an in-memory singleton - the next test needs a cold app
        this.App.RequestRelaunch();
        var toggle = (await this.Ui.Query(selector: $"{Page} Switch")).Single();
        await this.Ui.SetProperty(toggle.Id, "IsToggled", "true");
    }

    [Fact]
    public async Task Continue_lets_the_navigation_through()
    {
        await this.Ui.TapButton("Push Detail (guarded)");
        await this.Ui.TapButton("Let it through");

        await this.Ui.WaitForPage(Detail);
        await this.Ui.WaitForLabel(Detail, "Text: Pushed past the interceptor");
        await this.Ui.WaitForLabel(Page, "Last result: navigated (or redirected)");
    }

    [Fact]
    public async Task Redirect_goes_somewhere_else_and_reports_success()
    {
        await this.Ui.TapButton("Push Detail (guarded)");
        await this.Ui.TapButton("Redirect to Lifecycle");

        await this.Ui.WaitForPage("LifecycleDemoPage");
        (await this.Ui.Exists(Detail)).ShouldBeFalse("the redirected Detail page is never built");
        await this.Ui.WaitForLabel(Page, "Last result: navigated (or redirected)");
    }

    [Fact]
    public async Task Cancel_stays_put_and_NavigateTo_returns_false()
    {
        await this.Ui.TapButton("Push Detail (guarded)");
        await this.Ui.TapButton("Stop navigation");

        await this.Ui.WaitForLabel(Page, "Last result: cancelled by an interceptor");
        (await this.Ui.Exists(Detail)).ShouldBeFalse();
    }

    [Fact]
    public async Task The_sheet_sees_the_destination_ViewModel_already_populated()
    {
        await this.Ui.TapButton("Push Detail (guarded)");

        // The interceptor's title is built from the navigation in flight: from -> to (Text: '...')
        await this.Ui.WaitUntil(
            async () => (await this.Ui.Query(type: "Label")).Any(l => l.Text?.EndsWith("(Text: 'Pushed past the interceptor')", StringComparison.Ordinal) == true),
            "the sheet's title with the destination ViewModel's Text"
        );
        await this.Ui.TapButton("Stop navigation");
    }

    [Fact]
    public async Task Bypassing_interceptors_skips_the_sheet()
    {
        await this.Ui.TapButton("Push Detail (bypass interceptors)");

        await this.Ui.WaitForPage(Detail);
        await this.Ui.WaitForLabel(Detail, "Text: Bypassed the interceptors");
        (await this.Ui.Query(type: "Button")).ShouldNotContain(b => b.Text == "Let it through");
    }
}
