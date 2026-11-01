using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>
/// SetTabBadge / ClearTabBadge, read back through ShellTabBadgeManager on the Badge page (the badge
/// itself is drawn natively, outside the MAUI tree). Badges need the Tabbed Shell; the page offers
/// to switch.
/// </summary>
public sealed class TabBadgeTests(AppFixture app) : UiTest(app)
{
    const string Page = "BadgeDemoPage";
    const string Question = "Tab badges require the Tabbed Shell. Switch now?";

    public override async ValueTask InitializeAsync() => await new HomeList(this.Ui).Open("Tab Badges");

    [Fact]
    public async Task Declining_the_shell_switch_sets_nothing()
    {
        await this.Ui.TapButton("Set Badge = 5");
        await new Dialogs(this.Ui).Answer(Question, "No");

        (await this.Ui.Exists("AppShell")).ShouldBeTrue();
        (await this.Ui.Labels(Page)).ShouldContain("Current badge: (none)");
    }

    [Fact]
    public async Task Sets_changes_and_clears_the_badge_on_the_tabbed_shell()
    {
        await this.Ui.TapButton("Set Badge = 5");
        await new Dialogs(this.Ui).Answer(Question, "Yes");
        await this.Ui.WaitUntil(() => this.Ui.Exists("TabbedShell"), "the Tabbed Shell");

        // The page that asked went with the old Shell - the Badges tab has a new one
        await this.Ui.Navigate("//BadgeDemoPage");
        await this.Ui.WaitForLabel(Page, "Current badge: 5");

        await this.Ui.TapButton("Set Badge = 99");
        await this.Ui.WaitForLabel(Page, "Current badge: 99");

        await this.Ui.TapButton("Clear Badge");
        await this.Ui.WaitForLabel(Page, "Current badge: (none)");
    }
}
