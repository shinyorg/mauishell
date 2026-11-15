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

    // ToolbarItem.BadgeText is bound to this - toolbar items belong to the page, so the ViewModel drives
    // them through a binding rather than INavigator. null hides the badge, an empty string is a dot.
    [ObservableProperty] string? toolbarBadge;
    int toolbarCount;

    [RelayCommand]
    async Task SetBadge(string value)
    {
        if (!await this.EnsureTabbedShell())
            return;

        await navigator.SetTabBadge<BadgeDemoViewModel>(int.Parse(value));
        this.Refresh();
    }

    // "New", or "" for a dot
    [RelayCommand]
    async Task SetTextBadge(string text)
    {
        if (!await this.EnsureTabbedShell())
            return;

        await navigator.SetTabBadge<BadgeDemoViewModel>(text);
        this.Refresh();
    }

    [RelayCommand]
    void IncrementToolbarBadge() => this.ToolbarBadge = (++this.toolbarCount).ToString();

    [RelayCommand]
    void DotToolbarBadge() => this.ToolbarBadge = String.Empty;

    [RelayCommand]
    void ClearToolbarBadge()
    {
        this.toolbarCount = 0;
        this.ToolbarBadge = null;
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

    static string Describe(string? value) => value switch
    {
        null => "(none)",
        "" => "(dot)",
        _ => value
    };

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
