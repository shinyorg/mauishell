using Shiny;

namespace Sample;

[ShellMap<HomePage>(
    registerRoute: false,
    appLinks: ["home"],
    Shortcut = "Home",
    ShortcutSubtitle = "Back to the demo list",
    ShortcutOrder = 0
)]
public partial class HomeViewModel(INavigator navigator) : ObservableObject
{
    public ObservableCollection<DemoGroup> Demos { get; } =
    [
        new("Navigation")
        {
            new("Push & Pop", "Route and ViewModel-based navigation", () => navigator.NavigateTo(nameof(NavigationDemoPage))),
            new("XAML Declarative", "Navigate attached properties on buttons", () => navigator.NavigateTo(nameof(XamlNavDemoPage))),
            new("Builder Chain", "Fluent multi-segment navigation", () => navigator.NavigateTo(nameof(BuilderDemoPage))),
            new("Modal", "Modal page presentation", () => navigator.NavigateTo("modal")),
            new("Route Interceptor", "An action sheet decides: continue, redirect or cancel", () => navigator.NavigateTo(nameof(InterceptorDemoPage))),
        },
        new("Features")
        {
            new("Dialog Bench", "Every IDialogs provider and IDialogPresenter, switchable", () => navigator.NavigateTo(nameof(DialogDemoPage))),
            new("Overlay Host", "Dialogs raised from a ShinyContentPage", () => navigator.NavigateTo(nameof(OverlayHostDemoPage))),
            new("Tab Badges", "Set and clear tab badges", () => navigator.NavigateTo(nameof(BadgeDemoPage))),
            new("Lifecycle", "Page lifecycle and navigation events", () => navigator.NavigateTo(nameof(LifecycleDemoPage))),
        },
        new("AI")
        {
            new("AI Chat", "Chat with AI to navigate and fill forms", () => navigator.NavigateTo(nameof(AI.ChatPage))),
        },
        new("Advanced")
        {
            new("Shell Switching", "Swap between Shell types at runtime", () => navigator.NavigateTo(nameof(ShellTestPage))),
        }
    ];
}

public class DemoGroup(string name) : ObservableCollection<DemoItem>
{
    public string Name => name;
}

public record DemoItem(string Title, string Description, Action? Action = null);
