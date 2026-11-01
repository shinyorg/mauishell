using Sample.UITests.DevFlow;

namespace Sample.UITests.Pages;

/// <summary>HomePage's demo list. Each row's AutomationId is its title.</summary>
public sealed class HomeList(NativeUi ui)
{
    /// <summary>Row title → the page it opens.</summary>
    public static readonly (string Row, string Page)[] Demos =
    [
        ("Push & Pop", "NavigationDemoPage"),
        ("XAML Declarative", "XamlNavDemoPage"),
        ("Builder Chain", "BuilderDemoPage"),
        ("Modal", "ModalDemoPage"),
        ("Route Interceptor", "InterceptorDemoPage"),
        ("Dialog Bench", "DialogDemoPage"),
        ("Overlay Host", "OverlayHostDemoPage"),
        ("Tab Badges", "BadgeDemoPage"),
        ("Lifecycle", "LifecycleDemoPage"),
        ("Shell Switching", "ShellTestPage")
    ];

    /// <summary>Taps a row and waits for the page it opens.</summary>
    public async Task Open(string row)
    {
        var page = Demos.Single(d => d.Row == row).Page;
        await ui.TapGesture(row);
        await ui.WaitForPage(page);
    }
}
