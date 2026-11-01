using Shiny;
using Shiny.Infrastructure;

namespace Sample;

[ShellMap<BadgeDemoPage>]
public partial class BadgeDemoViewModel(
    INavigator navigator,
    IDialogs dialogs,
    ShellTabBadgeManager badges
) : ObservableObject
{
    // Read back from ShellTabBadgeManager rather than remembered here, so it shows what the
    // tab bar was actually given
    [ObservableProperty] string currentBadge = Describe(badges.Get(nameof(BadgeDemoPage)));

    [RelayCommand]
    async Task SetBadge(string value)
    {
        if (!await this.EnsureTabbedShell())
            return;

        await navigator.SetTabBadge<BadgeDemoViewModel>(int.Parse(value));
        this.Refresh();
    }

    [RelayCommand]
    async Task ClearBadge()
    {
        if (!await this.EnsureTabbedShell())
            return;

        await navigator.ClearTabBadge<BadgeDemoViewModel>();
        this.Refresh();
    }

    void Refresh() => this.CurrentBadge = Describe(badges.Get(nameof(BadgeDemoPage)));

    static string Describe(int? value) => value?.ToString() ?? "(none)";

    async Task<bool> EnsureTabbedShell()
    {
        if (Shell.Current is TabbedShell)
            return true;

        if (!await dialogs.Confirm("Switch Shell?", "Tab badges require the Tabbed Shell. Switch now?"))
            return false;

        await navigator.SwitchShell(new TabbedShell());
        return true;
    }
}
