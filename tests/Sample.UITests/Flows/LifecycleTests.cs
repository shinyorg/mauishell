using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>
/// LifecycleDemoViewModel logs IPageLifecycleAware / INavigationAware / INavigationConfirmation
/// calls into its own list. CanNavigate guards user-driven navigation only (tab, flyout, back
/// button), which DevFlow cannot drive - see NavigationTests.
/// </summary>
public sealed class LifecycleTests(AppFixture app) : UiTest(app)
{
    const string Page = "LifecycleDemoPage";
    const string Question = "Allow navigation away from Lifecycle page?";

    public override async ValueTask InitializeAsync()
    {
        await new HomeList(this.Ui).Open("Lifecycle");

        // Going home afterwards is a Shell-driven navigation, which the page's CanNavigate stops
        this.App.RequestRelaunch();
    }

    /// <summary>The event list without its "[HH:mm:ss] " prefix.</summary>
    async Task<IReadOnlyList<string>> Events()
        => (await this.Ui.Labels(Page))
            .Where(l => l.Length > 11 && l[0] == '[' && l[9] == ']')
            .Select(l => l[11..])
            .ToList();

    Task WaitForEvent(string e, int count = 1)
        => this.Ui.WaitUntil(
            async () => (await this.Events()).Count(x => x.StartsWith(e, StringComparison.Ordinal)) >= count,
            $"'{e}' x{count} in the lifecycle log"
        );

    [Fact]
    public async Task OnAppearing_fires_when_the_page_shows()
    {
        await this.WaitForEvent("OnAppearing");
    }

    [Fact]
    public async Task Pushing_on_top_runs_OnNavigatingFrom_and_OnDisappearing_then_OnAppearing_on_return()
    {
        await this.WaitForEvent("OnAppearing");
        await this.Ui.TapButton("Clear Log");

        await this.Ui.TapButton("Push Detail");
        await this.Ui.WaitForPage("DetailPage");
        await this.Ui.WaitForLabel("DetailPage", "Text: From Lifecycle");

        await this.Ui.TapButton("Go Back");
        await this.Ui.WaitForPageGone("DetailPage");
        await this.WaitForEvent("OnAppearing");

        // The app's own NavigateTo is not a user leaving, so CanNavigate is not asked
        var events = await this.Events();
        events.Select(e => e.Split(' ')[0]).ToArray().ShouldBe(["OnNavigatingFrom", "OnDisappearing", "OnAppearing"]);
    }
}
