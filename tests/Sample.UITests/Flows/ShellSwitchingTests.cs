using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

/// <summary>INavigator.SwitchShell - replacing the whole Shell at runtime.</summary>
public sealed class ShellSwitchingTests(AppFixture app) : UiTest(app)
{
    [Theory]
    [InlineData("Switch to Tabbed", "TabbedShell")]
    [InlineData("Switch to Flyout", "FlyoutShell")]
    public async Task Switches_to_another_shell_and_back(string button, string shell)
    {
        await new HomeList(this.Ui).Open("Shell Switching");
        await this.Ui.WaitForLabel("ShellTestPage", "Current: AppShell");

        await this.Ui.TapButton(button);
        await this.Ui.WaitUntil(() => this.Ui.Exists(shell), shell);
        await this.Ui.WaitUntil(async () => !await this.Ui.Exists("AppShell"), "AppShell to be replaced");
        await this.Ui.WaitForPage("HomePage");

        // The new Shell still navigates with the same INavigator
        await new HomeList(this.Ui).Open("Shell Switching");
        await this.Ui.WaitForLabel("ShellTestPage", $"Current: {shell}");
        await this.Ui.TapButton("Switch to Standard");
        await this.Ui.WaitUntil(() => this.Ui.Exists("AppShell"), "AppShell");
        await this.Ui.WaitUntil(async () => !await this.Ui.Exists(shell), $"{shell} to be replaced");
    }
}
