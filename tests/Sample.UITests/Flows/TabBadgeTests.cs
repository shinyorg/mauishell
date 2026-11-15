using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>
/// SetTabBadge / ClearTabBadge, read back through ShellTabBadgeManager on the Badge page (the badge
/// itself is drawn natively, outside the MAUI tree), and a ToolbarItem badge bound to the ViewModel. Badges need the Tabbed Shell; the page offers
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


    [Fact]
    public async Task Sets_text_and_dot_badges()
    {
        await this.Ui.TapButton("Set Badge = New");
        await new Dialogs(this.Ui).Answer(Question, "Yes");
        await this.Ui.WaitUntil(() => this.Ui.Exists("TabbedShell"), "the Tabbed Shell");

        await this.Ui.Navigate("//BadgeDemoPage");
        await this.Ui.WaitForLabel(Page, "Current badge: New");

        await this.Ui.TapButton("Set Badge = Dot");
        await this.Ui.WaitForLabel(Page, "Current badge: (dot)");

        await this.Ui.TapButton("Clear Badge");
        await this.Ui.WaitForLabel(Page, "Current badge: (none)");
    }


    [Fact]
    public async Task Toolbar_badge_follows_the_viewmodel_without_a_shell_switch()
    {
        await this.Ui.TapButton("Increment Toolbar Badge");
        await this.Ui.WaitForLabel(Page, "Toolbar badge: 1");

        await this.Ui.TapButton("Increment Toolbar Badge");
        await this.Ui.WaitForLabel(Page, "Toolbar badge: 2");

        await this.Ui.TapButton("Clear Toolbar Badge");
        await this.Ui.WaitForLabel(Page, "Toolbar badge: (none)");
        (await this.Ui.Exists("AppShell")).ShouldBeTrue();
    }
}
