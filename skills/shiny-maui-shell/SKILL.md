---
name: shiny-maui-shell
description: Generate .NET MAUI Shell pages, ViewModels, navigation, and source-generated routes using Shiny MAUI Shell
auto_invoke: true
triggers:
  - maui shell
  - shell navigation
  - xaml navigation
  - attached navigation
  - tab badge
  - badge
  - shell switch
  - switch shell
  - maui navigation
  - maui page
  - maui viewmodel
  - INavigator
  - IDialogs
  - ShellMap
  - ShellProperty
  - appLinks
  - app link
  - app links
  - applink
  - deep link
  - deeplink
  - universal link
  - custom url scheme
  - url scheme
  - IAppLinks
  - UseAppLinks
  - AppLinkOptions
  - AppLinkRegistry
  - AppLinkRoutes
  - AppLinkMatch
  - ShinyAppLinkSchemes
  - ShinyAppLinkDomains
  - intent filter
  - associated domains
  - CFBundleURLTypes
  - apple-app-site-association
  - assetlinks.json
  - app shortcut
  - app shortcuts
  - quick action
  - quick actions
  - home screen shortcut
  - AppActions
  - AppAction
  - AddAppShortcut
  - ShortcutIcon
  - ShortcutSubtitle
  - ShortcutOrder
  - IAppShortcutText
  - UseShortcutText
  - IAppShortcuts
  - shortcut localization
  - UIApplicationShortcutItem
  - ShortcutManager
  - UseShinyShell
  - UseDialogs
  - UseShinyDialogs
  - ShinyDialogs
  - UseUxDiversDialogs
  - UseShinyDialogPresenter
  - UseUxDiversDialogPresenter
  - ShinyOverlayDialogPresenter
  - UxDiversDialogPresenter
  - ViewDialogPresenter
  - ShinyShell
  - ShellServices
  - ShinyAppBuilder
  - IMainThread
  - Shiny.Maui.Shell
  - IPageLifecycleAware
  - INavigationConfirmation
  - INavigationAware
  - NavigateTo
  - GoBack
  - PopToRoot
  - SetRoot
  - SetTabBadge
  - ClearTabBadge
  - SwitchShell
  - CreateBuilder
  - INavigationBuilder
  - NavigationBuilder
  - Navigating
  - Navigated
  - INavigationInterceptor
  - NavigationInterceptorResult
  - AddNavigationInterceptor
  - navigation interceptor
  - navigation guard
  - navigation middleware
  - cancel navigation
  - redirect navigation
  - auth guard
  - route guard
  - INavigationContextAccessor
  - NavigationContext
  - NavigationDirection
  - bypassInterceptors
  - AppLinkResult
  - IDialogs
  - Alert
  - Confirm
  - Prompt
  - ActionSheet
  - IDialogAware
  - DialogResult
  - IDialogPresenter
  - ShowDialog
  - UseDialogPresenter
  - viewmodel dialog
  - dialog result
  - NavigationEventArgs
  - NavigatedEventArgs
  - Navigate.Route
  - Navigate.RelativeNavigation
  - Navigate.ParameterKey
  - Navigate.ParameterValue
  - Navigate.Parameters
  - NavigationParameters
  - NavigationParameter
  - GetGeneratedRouteInfo
  - GetAiToolApplicableGeneratedRoutes
  - NavigateToRoute
  - GeneratedRouteInfo
  - GeneratedRouteParameter
  - AI navigation
  - AI tool
  - chat navigation
  - AiMauiShellTools
  - AddAiTools
---

# Shiny MAUI Shell Skill

You are an expert in Shiny MAUI Shell, a library that enhances .NET MAUI Shell with ViewModel lifecycle management, navigation services, source generation, tab badges, and XAML-triggered navigation.

## When to Use This Skill

Invoke this skill when the user wants to:
- Create new MAUI pages with ViewModels using Shiny Shell conventions
- Set up or configure Shiny MAUI Shell in their application
- Switch between different Shell instances at runtime (e.g., login shell vs main app shell)
- Implement navigation between pages using `INavigator`
- Set or clear tab badge values on tabs in the active Shell
- Add route-based XAML navigation with `Navigate.*` attached properties
- Build multi-segment navigation chains using `INavigationBuilder` (push multiple pages, pop-and-push)
- Show dialogs (alert, confirm, prompt, action sheet) using `IDialogs`
- Add ViewModel lifecycle hooks (appearing, disappearing, navigation confirmation)
- Use source generation with `[ShellMap]` and `[ShellProperty]` attributes
- Pass parameters between pages during navigation
- Create modal pages or tab navigation
- Migrate from vanilla MAUI Shell or Prism navigation to Shiny MAUI Shell
- Set up AI-driven navigation using `Microsoft.Extensions.AI` with route discovery and `NavigateToRoute`
- Create AI-compatible ViewModels with descriptive `[ShellMap]` and `[ShellProperty]` attributes

## Library Overview

**Documentation**: https://shinylib.net/maui
**GitHub**: https://github.com/shinyorg/mauishell
**NuGet**: `Shiny.Maui.Shell`
**Namespace**: `Shiny`

Shiny MAUI Shell wraps .NET MAUI Shell to provide:
- Page-to-ViewModel registration and automatic BindingContext assignment
- A testable `INavigator` service for all navigation operations
- A testable `IDialogs` service for alert, confirm, prompt, and action sheet dialogs
- `INavigationBuilder` for multi-segment navigation (push multiple pages in one operation, pop-and-push)
- Native numeric tab badges via `INavigator.SetTabBadge*` / `ClearTabBadge*`
- Attached-property XAML navigation via `Navigate.Route`, `Navigate.RelativeNavigation`, and parameter helpers
- Shell switching — swap the entire Shell at runtime (e.g., login → main app)
- ViewModel lifecycle interfaces (appearing, disappearing, dispose, navigation confirmation)
- `INavigationInterceptor` guards that can cancel or redirect any navigation - including app links, shortcuts and tab taps
- Source generators that eliminate boilerplate route registration, produce strongly-typed navigation methods, and generate AI tool metadata
- `ShinyShell` base class for deterministic initial-page BindingContext assignment
- `ShellServices` record that aggregates `INavigator`, `IDialogs`, and `IMainThread` for convenient single-parameter injection
- `IMainThread` abstraction with built-in workarounds for macOS and Linux where `MainThread.InvokeOnMainThreadAsync` can deadlock / fail
- Pluggable `IDialogs` implementation via `UseDialogs<TDialog>()` — swap in your own dialog provider (e.g. ACR UserDialogs, a custom sheet, a test double)

Inspired by [Prism Library](https://prismlibrary.com) by Dan Siegel and Brian Lagunas.

## Setup

### 1. Install NuGet Package
```bash
dotnet add package Shiny.Maui.Shell
```

### 2. Configure in MauiProgram.cs

**Manual registration:**
```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseShinyShell(x => x
            .Add<MainPage, MainViewModel>(registerRoute: false)
            .Add<DetailPage, DetailViewModel>("Detail")
            .Add<SettingsPage, SettingsViewModel>("Settings")
        )
        .ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        });

    return builder.Build();
}
```

**With source generation (preferred):**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .AddGeneratedMaps()
        .AddAiTools()       // registers AiMauiShellTools as singleton for AI integration
    )
```

**With a custom dialog provider:**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .AddGeneratedMaps()
        .UseDialogs<MyCustomDialogs>()   // register a custom IDialogs implementation
    );
```

`UseDialogs<TDialog>()` replaces the default `ShellDialogs` provider. The default registration uses `TryAddSingleton`, so a `UseDialogs<>` call always wins.

**Built-in alternative providers** (same `IDialogs` interface — no ViewModel changes):
```csharp
// Owned, animated, themeable dialogs via Shiny.Maui.Controls (package: Shiny.Maui.Shell.ShinyDialogs)
builder
    .UseMauiApp<App>()
    .UseShinyControls()                  // registers the Controls IDialogService
    .UseShinyShell(x => x
        .AddGeneratedMaps()
        .UseShinyDialogs()               // IDialogs -> Controls dialog service
        .UseShinyDialogPresenter()       // ShowDialog -> overlay card (optional, see 6a)
    );

// Styled UXDivers popups (package: Shiny.Maui.Shell.UxDiversDialogs)
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .AddGeneratedMaps()
        .UseUxDiversDialogs()            // IDialogs -> UXDivers popups
        .UseUxDiversDialogPresenter()    // ShowDialog -> UXDivers popup (optional, see 6a)
    );
```

Either UXDivers call initializes the popup infrastructure (`UseUXDiversPopups()`) itself — do NOT
also call `builder.UseUXDiversPopups()`, and calling both Shiny extensions initializes it once.

### 3. AppShell must inherit from `ShinyShell`

Your `AppShell` (or any Shell subclass) must inherit from `Shiny.ShinyShell` instead of `Shell`. This ensures the initial page's BindingContext is set deterministically via Shell's own `OnNavigated` lifecycle.

**AppShell.xaml:**
```xml
<shiny:ShinyShell
    x:Class="MyApp.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell"
    xmlns:local="clr-namespace:MyApp"
    Title="MyApp">

    <ShellContent
        Title="Home"
        ContentTemplate="{DataTemplate local:MainPage}"
        Route="MainPage" />

</shiny:ShinyShell>
```

**AppShell.xaml.cs:**
```csharp
using Shiny;

namespace MyApp;

public partial class AppShell : ShinyShell
{
    public AppShell()
    {
        InitializeComponent();
    }
}
```

### Important Notes
- Pages defined in AppShell.xaml should use `registerRoute: false` since Shell already registers them
- Pages navigated to programmatically need route registration (the default behavior)
- All Pages and ViewModels are registered as Transient in DI automatically

## Code Generation Instructions

When generating code for Shiny MAUI Shell projects, follow these conventions:

### 1. ViewModels

All ViewModels must implement `INotifyPropertyChanged`. Use `CommunityToolkit.Mvvm` `ObservableObject` as the base:

```csharp
[ShellMap<MyPage>("MyRoute")]
public partial class MyViewModel : ObservableObject
{
}
```

- Use `[ShellMap<TPage>("Route")]` on every ViewModel class
- The `route` parameter must be a valid C# identifier — it is used as the generated constant name and method name
- Invalid route names (hyphens, spaces, leading digits) produce a **SHINY001** compiler error
- When no route is specified, the page type name without the `Page` suffix is used as the generated name
- Set `registerRoute: false` only for pages already declared in AppShell.xaml
- ViewModel classes using source generation should be `partial`
- Use primary constructors to inject `INavigator` and other dependencies

### 2. Navigation Properties

Use `[ShellProperty]` on ViewModel properties that should be passed as navigation parameters:

```csharp
[ShellMap<DetailPage>("Detail")]
public partial class DetailViewModel : ObservableObject
{
    [ShellProperty]
    public string ItemId { get; set; }

    [ShellProperty(required: false)]
    public int PageIndex { get; set; }
}
```

- Properties marked `[ShellProperty]` are required by default
- Use `[ShellProperty(required: false)]` for optional parameters
- `[ShellProperty]` properties are set directly by the source-generated navigation methods — no `IQueryAttributable` needed
- Source generator creates strongly-typed extension methods on `INavigator`

### 3. Lifecycle Interfaces

Implement these interfaces on ViewModels as needed:

| Interface | Purpose |
|-----------|---------|
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` hooks |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` - confirm before leaving. **Only asked for user-driven Shell navigation** (tab tap, flyout, hardware back), not for `INavigator` calls - use an `INavigationInterceptor` to guard those |
| `INavigationAware` | `OnNavigatingFrom(IDictionary<string, object>)` - mutate args before leaving |
| `IQueryAttributable` | `ApplyQueryAttributes(IDictionary<string, object>)` - receive navigation args (only needed for string-based `NavigateTo(route, args)` — not needed when using `[ShellProperty]`) |
| `IDisposable` | Cleanup when page is removed from navigation stack |

### 4. Navigation Events

`INavigator` exposes two events for observing navigation:

- `Navigating` — fires **before** navigation with the source ViewModel instance
- `Navigated` — fires **after** navigation with the destination ViewModel instance

```csharp
navigator.Navigating += (sender, args) =>
{
    // args.FromUri, args.FromViewModel, args.ToUri, args.NavigationType, args.Parameters
};

navigator.Navigated += (sender, args) =>
{
    // args.ToUri, args.ToViewModel, args.NavigationType, args.Parameters
};
```

Hook these events in an `IMauiInitializeService` for cross-cutting concerns like logging or analytics.

### 4a. Navigation Interceptors (guards)

Use `INavigationInterceptor` when navigation must be **blocked or rerouted** - auth guards, unsaved
changes, feature flags. Use the `Navigating`/`Navigated` events when you only need to observe.

```csharp
public class AuthNavigationInterceptor(IAuthService auth) : INavigationInterceptor
{
    public int Order => -100;   // guards before observers; ties keep registration order

    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        if (await auth.IsAuthorized(cancellationToken) || uri.Contains("Login"))
            return NavigationInterceptorResult.Continue;

        return NavigationInterceptorResult.Redirect<LoginViewModel>();
    }
}
```

Register in `MauiProgram.cs` - they run in registration order, first to cancel or redirect wins:

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddNavigationInterceptor<AuthNavigationInterceptor>()
    .AddNavigationInterceptor<AuditNavigationInterceptor>()
    // inline for one-liners
    .AddNavigationInterceptor((uri, vm, ct) => Task.FromResult(NavigationInterceptorResult.Continue), order: 100)
);
```

**Results:**

| Result | Behaviour |
|:-------|:----------|
| `NavigationInterceptorResult.Continue` | Next interceptor, then navigate |
| `Cancel()` | Nothing navigates; the caller's `Task` completes normally |
| `Redirect("Detail")` | Push |
| `Redirect("//Main/Home")` / `Redirect("/Login")` | Reset the stack (single leading `/` is promoted to `//`) |
| `Redirect<LoginViewModel>()` | Reset the stack to that ViewModel's route - **prefer this**, it is refactor-safe |
| `Redirect<DetailViewModel>(relativeNavigation: true)` | Push that ViewModel's route |

**Rules to generate correctly:**

- The `viewModel` argument is the **destination** ViewModel, already resolved and populated
  (`configure` callback run, app link values applied). Mutating it is allowed - that instance is
  bound to the page, except on a `registerRoute: false` (ShellContent) page that is already
  realised, which keeps the ViewModel it already has. It is `null` for unmapped routes and for Shell-driven navigation (tab taps,
  hardware back), so always null-check or pattern-match: `if (viewModel is DetailViewModel vm)`.
- The ViewModel being **left** comes from `INavigationContextAccessor.Current.FromViewModel` -
  inject `INavigationContextAccessor` for that, plus `FromUri`, `ToUri`, `NavigationType`,
  `Parameters`, `RedirectCount`.
- A redirect re-runs the whole chain against the new URI. Guard against redirecting to the page you
  are guarding (check the URI or ViewModel type first), or it just gets ignored; a real loop throws
  after 10 hops.
- Interceptors cover `INavigator` calls, the navigation builder, back navigation, app links, app
  shortcuts and user-driven Shell navigation. They do **not** cover `ShowDialog` or `SwitchShell`.
- Anything thrown propagates to the caller and the navigation does not happen.
- Interceptors are registered as singletons - do not hold per-navigation state in fields.
- Ordering is `Order` (lowest first) then registration order. Put guards below 0 and observers above.
- Every `INavigator` navigation method returns `Task<bool>` - false means an interceptor cancelled
  it (a redirect returns true). Generated `NavigateTo{Route}` methods return `Task<bool>` too.
- To navigate **from inside** a guard, or for any navigation that must not be guarded, pass
  `bypassInterceptors: true`: `NavigateTo<LoginViewModel>(bypassInterceptors: true)`,
  `GoBack(1, bypassInterceptors: true)`, `PopToRoot(bypassInterceptors: true)`,
  `CreateBuilder()...Navigate(bypassInterceptors: true)` or the fluent
  `CreateBuilder().BypassInterceptors()...Navigate()`. A `RedirectUri` does not need it.
- `INavigationContextAccessor.Current.Direction` gives `Forward` / `Back` / `Root` when the rule
  only cares which way the user is going; `.GetDirection()` converts any `NavigationType`.
- An inbound app link a guard blocks reports `AppLinkResult.Blocked` from `IAppLinks.Handle`
  (distinct from `Unhandled`, which means nothing matched).
- A dialog can be awaited inside an interceptor (`IDialogs.Confirm` / `ActionSheet`) - the
  navigation waits on the answer. Treat a dismissed sheet (which returns the `cancel` text) as
  `Cancel()`, and always narrow to the destination being guarded first.

```csharp
// Unsaved-changes guard: uses the page being left, not the destination
public class UnsavedChangesInterceptor(
    INavigationContextAccessor context,
    IDialogs dialogs
) : INavigationInterceptor
{
    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        if (context.Current?.FromViewModel is not IUnsavedChanges { HasUnsavedChanges: true })
            return NavigationInterceptorResult.Continue;

        return await dialogs.Confirm("Unsaved Changes", "Discard changes?")
            ? NavigationInterceptorResult.Continue
            : NavigationInterceptorResult.Cancel();
    }
}
```

```csharp
// One action sheet, all three outcomes - a dialog is awaitable here because the navigation has
// not been handed to Shell yet
public class AskFirstInterceptor(IDialogs dialogs) : INavigationInterceptor
{
    const string LetItGo = "Let it through";
    const string SendElsewhere = "Go to Settings instead";
    const string StopIt = "Stop navigation";

    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        if (viewModel is not DetailViewModel)   // always narrow, or tab taps prompt too
            return NavigationInterceptorResult.Continue;

        var choice = await dialogs.ActionSheet(
            $"Navigating to '{uri}'",
            cancel: StopIt,          // a dismissed sheet returns the cancel text
            destruction: null,
            buttons: [LetItGo, SendElsewhere]
        );

        return choice switch
        {
            LetItGo => NavigationInterceptorResult.Continue,
            SendElsewhere => NavigationInterceptorResult.Redirect<SettingsViewModel>(relativeNavigation: true),
            _ => NavigationInterceptorResult.Cancel()   // cancel, dismissed or unknown
        };
    }
}
```

### 5. Navigation

Always use `INavigator` for navigation, never `Shell.Current.GoToAsync` directly:

```csharp
// Route-based navigation with args
await navigator.NavigateTo("Detail", args: [("ItemId", "123"), ("PageIndex", 0)]);

// ViewModel-based navigation with strongly-typed configuration
await navigator.NavigateTo<DetailViewModel>(vm => vm.ItemId = "123");

// Source-generated strongly-typed method (preferred)
await navigator.NavigateToDetail("123", pageIndex: 0);

// Absolute navigation (navigates to root route "//Detail")
await navigator.NavigateTo("Detail", relativeNavigation: false);
await navigator.NavigateTo<DetailViewModel>(relativeNavigation: false);

// Go back with result parameters
await navigator.GoBack(("Result", selectedItem));

// Go back multiple pages
await navigator.GoBack(backCount: 2);

// Pop to root
await navigator.PopToRoot();

// Switch to a different Shell instance
await navigator.SwitchShell(new MainAppShell());

// Switch to a Shell resolved from DI
await navigator.SwitchShell<MainAppShell>();
```

### 5a. Navigation Builder

Use `INavigationBuilder` for multi-segment navigation (pushing multiple pages in a single operation):

```csharp
// Push a chain of pages: One > Another > Two
await navigator
    .CreateBuilder()
    .Add<OneViewModel>(x => x.Text = "First")
    .Add<AnotherViewModel>(x => x.Arg = "Middle")
    .Add<TwoViewModel>(x => x.Text = "Last")
    .Navigate();

// Pop back 2 pages, then push a new page
await navigator
    .CreateBuilder()
    .PopBack(2)
    .Add<OneViewModel>(x => x.Text = "Replaced")
    .Navigate();

// Add by route name (no configure callback)
await navigator
    .CreateBuilder()
    .Add("Detail")
    .Navigate();
```

**Important Shell constraints for the Navigation Builder:**
- All pages used in a builder chain **must** be globally registered via `Routing.RegisterRoute` (i.e., `registerRoute: true`, which is the default). Pages declared as `ShellContent` in XAML cannot be used in multi-segment relative URIs.
- `PopBack()` must be called before any `Add()` calls.
- `fromRoot: true` on `CreateBuilder` only works when the target route is a shell-declared route (a `ShellContent` in XAML), not a globally registered route.

### 5b. Tab Badges

Use the badge APIs when a route already exists as a tab in the active Shell:

```csharp
// Route-based badge updates
await navigator.SetTabBadge("Inbox", 3);
await navigator.ClearTabBadge("Inbox");

// ViewModel-based badge updates
await navigator.SetTabBadge<InboxViewModel>(7);
await navigator.ClearTabBadge<InboxViewModel>();
```

- Badge APIs target existing tabs in the active Shell, not arbitrary pushed pages
- Supported platforms: Android, iOS, Mac Catalyst, Windows
- Unsupported platforms currently throw `PlatformNotSupportedException` (neutral target, Linux, macOS AppKit)

### 5c. XAML Navigation

Use `Navigate.*` attached properties for simple route-based navigation directly from XAML:

```xml
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell">
    <ContentPage.ToolbarItems>
        <ToolbarItem Text="Home"
                     shiny:Navigate.Route="MainPage"
                     shiny:Navigate.RelativeNavigation="False" />
    </ContentPage.ToolbarItems>

    <Button Text="Open Detail"
            shiny:Navigate.Route="Detail"
            shiny:Navigate.ParameterKey="ItemId"
            shiny:Navigate.ParameterValue="{Binding SelectedId}" />
</ContentPage>
```

For multiple parameters:

```xml
<Button Text="Open Modal"
        shiny:Navigate.Route="Modal">
    <shiny:Navigate.Parameters>
        <shiny:NavigationParameters>
            <shiny:NavigationParameter Key="Arg1" Value="{Binding NavArg}" />
            <shiny:NavigationParameter Key="Arg2" Value="5" />
        </shiny:NavigationParameters>
    </shiny:Navigate.Parameters>
</Button>
```

- Supported controls: `Button`, `MenuItem`, `ToolbarItem`
- `Navigate.Route` accepts the route string passed to `INavigator.NavigateTo(...)`
- Keep XAML navigation generic; strongly-typed helpers belong in generated C# extensions, not XAML attached properties

### 5d. AI Tool Navigation

Shiny MAUI Shell generates AI-compatible route metadata and navigation methods for use with `Microsoft.Extensions.AI`. An AI chat client can discover routes, understand their purpose, and navigate with parameters extracted from natural language.

**Describe routes for AI** — Add `description` to `[ShellMap]` and `[ShellProperty]`:

```csharp
public enum WorkOrderPriority { Low, Medium, High, Urgent }

[ShellMap<WorkOrderPage>(description: "Use when the user reports something broken or needing repair")]
public partial class WorkOrderViewModel : ObservableObject
{
    [ShellProperty("Summarize what is broken based on what the user said", required: true)]
    public string Description { get; set; } = string.Empty;

    [ShellProperty("Infer urgency from tone. Must be: Low, Medium, High, or Urgent", required: true)]
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
}
```

**Generated AI class (`AiMauiShellTools`):**

The source generator produces an `AiMauiShellTools` class (name configurable via `ShinyMauiShell_AiToolsClassName`) that takes `INavigator` via constructor injection and provides:

- `Prompt` — pre-formatted string describing all AI-applicable routes for seeding AI system messages
- `Tools` — ready-to-use `AITool[]` instances for route discovery and navigation
- `GetAiToolApplicableGeneratedRoutes()` — returns only routes that have a description AND at least one parameter
- `NavigateToRoute(route, args)` — AI-friendly navigation using switch dispatch to `NavigateTo<TViewModel>` with direct property setters and automatic type conversion (`int`, `bool`, `double`, enums, `DateTime`, etc.)

Additionally, `GetGeneratedRouteInfo()` remains as a static extension on `INavigator` returning all routes with parameter metadata.

A generated `AddAiTools()` extension on `ShinyAppBuilder` registers the class as a singleton.

**Wire up AI tools (enabled by default when `Microsoft.Extensions.AI` is referenced):**

```csharp
// MauiProgram.cs
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddAiTools()
);

// In your ViewModel — inject AiMauiShellTools
public class ChatViewModel(AiMauiShellTools aiTools)
{
    var options = new ChatOptions { Tools = [.. aiTools.Tools] };
}
```

**Key conventions for AI-friendly ViewModels:**
- Route descriptions should describe **user intent signals**, not just the page name — e.g. "Use when the user reports something broken" not "Work order page"
- Property descriptions should tell the AI to **infer values** from natural language — e.g. "Infer urgency from tone" not "The priority level"
- Use `GetAiToolApplicableGeneratedRoutes` (not `GetGeneratedRouteInfo`) to keep the AI focused on actionable routes
- Properties can be `string`, `int`, `bool`, `double`, enums, `DateTime`, `Guid`, etc. — the generated `NavigateToRoute` handles type conversion automatically
- Enums are especially AI-friendly — the model outputs the member name as a string and the generator parses it case-insensitively

### 5e. App Links (deep links)

Declare inbound URL templates in the `appLinks` argument of `[ShellMap]` - never a separate attribute:

```csharp
[ShellMap<ProductPage>(
    description: "Shows a product",
    appLinks: ["product/{id}", "p/{id}"]
)]
public partial class ProductViewModel : ObservableObject
{
    [ShellProperty("The product id")] public int     Id  { get; set; }
    [ShellProperty(required: false)]  public string? Tab { get; set; }
}
```

Wire it up once:

```xml
<PropertyGroup>
  <ShinyAppLinkSchemes>myapp</ShinyAppLinkSchemes>
  <ShinyAppLinkDomains>shinylib.net</ShinyAppLinkDomains>
</PropertyGroup>
```

```csharp
.UseShinyShell(x => x.AddGeneratedMaps())
```

`AddGeneratedMaps()` installs everything declared on `[ShellMap]` - routes, app links and app
shortcuts. There is no separate opt-in call.

**Rules to follow when generating app link code:**

1. `{token}` names must match a `[ShellProperty]` name (case-insensitive) or the build fails with
   **SHINY005**. Query values bind by property name too.
2. Do **not** add a `navigationRoot` or nav-mode argument - push vs. reset is inferred from
   `registerRoute`. `registerRoute: false` (a `ShellContent` in AppShell XAML) resets to `//route`;
   `registerRoute: true` pushes.
3. Do **not** hand-write URL parsing, an `Application` subclass, an `AppDelegate` `OpenUrl`
   override, or a `MainActivity` `OnNewIntent` override. `AddGeneratedMaps()` installs all of it.
   `UseAppLinks(o => ...)` is optional and only changes `AppLinkOptions` defaults.
4. Templates carry no scheme or host - any configured scheme or domain serves any template.
5. Manifest entries are **not** generated. The build emits SHINY101-105 warnings containing the
   exact markup; put `[IntentFilter]` on `MainActivity` for Android, `CFBundleURLTypes` in
   `Info.plist` and `com.apple.developer.associated-domains` in `Entitlements.plist` for Apple.
6. To build an outbound URL use the generated `Create{Route}AppLink(...)`, not string concatenation.
   It is only generated when exactly one scheme (or one domain, with no scheme) is configured.

Optional tuning:

```csharp
.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseAppLinks(o =>
    {
        o.DefaultRoot = "//main/home";           // back stack for cold-start pushes
        o.ResolveRoute = match => "//custom";    // overrides the registerRoute inference
        o.OnUnhandled = uri => Task.FromResult(false);
    })
)
```

### 5f. App Shortcuts (home screen quick actions)

Declared with **named properties on `[ShellMap]`** - there is no separate attribute:

```csharp
[ShellMap<SearchPage>(
    Shortcut         = "Search",
    ShortcutSubtitle = "Find anything",
    ShortcutIcon     = "search",
    ShortcutOrder    = 0
)]
public partial class SearchViewModel : ObservableObject { }
```

```csharp
.UseShinyShell(x => x.AddGeneratedMaps())
```

**Rules to follow when generating app shortcut code:**

1. `Shortcut` (the title) is the trigger. Setting `ShortcutSubtitle`/`ShortcutIcon`/`ShortcutOrder`
   without it is a **SHINY012** error - nothing would be declared.
2. A route with a **required** `[ShellProperty]` cannot declare a shortcut (**SHINY010**). Use
   `builder.AddAppShortcut<TViewModel>(title, configure: vm => vm.X = ..., id: "...")` instead.
3. At most four shortcuts (**SHINY011** warns) - iOS drops the excess silently.
4. Do **not** write platform code, and do **not** emit an opt-in call for shortcuts - none
   exists. `AddGeneratedMaps()` installs shortcuts. MAUI's `AppActions` handles delivery; do not
   hand-write `UIApplicationShortcutItem`, `ShortcutManagerCompat`,
   `ConfigureEssentials`/`AddAppAction`, or an `OnAppAction` switch.
5. Do **not** add a navigation-mode argument - push vs. reset is inferred from `registerRoute`,
   same as app links.
6. `AddAppShortcut<TViewModel>` is the public API the generator emits calls to; it is also how to
   use shortcuts when source generation is disabled.
7. For **localized** titles/subtitles, implement `IAppShortcutText` and register it with
   `UseShortcutText<T>()` - the declared string becomes the resource key. Do not try to localize by
   putting `CultureInfo` logic in the attribute; attribute arguments are compile-time constants.
   After a language change call `IAppShortcuts.Refresh()`, or the installed text stays stale.

### 6. Dialogs

Always use `IDialogs` for user-facing dialogs. Inject it via the primary constructor:

```csharp
public class MyViewModel(INavigator navigator, IDialogs dialogs)
{
    // Alert - informational message
    await dialogs.Alert("Title", "Something happened");

    // Confirm - yes/no question, returns bool
    bool confirmed = await dialogs.Confirm("Delete?", "Are you sure?");

    // Prompt - text input, returns string? (null if cancelled)
    var name = await dialogs.Prompt("Name", "Enter your name", placeholder: "John Doe");

    // Prompt with numeric keyboard
    var age = await dialogs.Prompt("Age", "Enter your age", keyboard: Keyboard.Numeric);

    // Action sheet - choose from options
    var choice = await dialogs.ActionSheet("Options", "Cancel", "Delete", "Edit", "Share");
}
```

### 6a. ViewModel Dialogs (awaiting a typed result from a page)

Use `IDialogs` for alert / confirm / prompt / action sheet. When the dialog needs real UI — a picker,
a filter sheet, a form — build a normal Page + ViewModel pair, have the ViewModel implement
`IDialogAware<T>`, and await it with the generated `Show{Route}Dialog` extension.

**ViewModel** — implement `IDialogAware<T>` with two events. Do NOT introduce a base class; the
ViewModel's base slot belongs to `ObservableObject`.

```csharp
[ShellMap<PickColorPage>("PickColor")]
public partial class PickColorViewModel : ObservableObject, IDialogAware<string>
{
    public event EventHandler<string>? Completed;
    public event EventHandler? Cancelled;

    [ShellProperty("The colour to pre-select", required: false)]
    public string Preset { get; set; } = "Red";

    [RelayCommand] void Pick(string colour) => this.Completed?.Invoke(this, colour);
    [RelayCommand] void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);
}
```

**Call site** — always prefer the generated extension. It infers both type arguments and turns
`[ShellProperty]` values into method parameters:

```csharp
var result = await navigator.ShowPickColorDialog(preset: "Violet");

if (result.TryGetValue(out var colour))
    this.Selected = colour;

// or, with a fallback
this.Selected = result.ValueOr("Red");
```

The underlying method needs both type arguments spelled out, because C# cannot infer a type argument
from a constraint — only reach for it when there is no `[ShellMap]` on the ViewModel:

```csharp
var result = await navigator.ShowDialog<PickColorViewModel, string>(x => x.Preset = "Violet");
```

**Rules:**
- The dialog ViewModel must be mapped to a page, exactly like a navigable ViewModel — `[ShellMap<TPage>]`
  + `AddGeneratedMaps()`, or `ShinyAppBuilder.Add<TPage, TViewModel>()`. It throws otherwise.
- `DialogResult<T>` — check `IsCancelled`, or use `TryGetValue(out var v)` / `ValueOr(fallback)`.
  Never assume `Value` is meaningful without checking; `default(T)` cannot represent cancellation for
  value types.
- User dismissal (hardware back, iOS swipe-down) returns a cancelled `DialogResult<T>`. The passed
  `CancellationToken` firing throws `OperationCanceledException` — these are different things.
- The dialog page does NOT need `Shell.PresentationMode="Modal"` — the default presenter pushes it modally.
- Lifecycle: `IPageLifecycleAware.OnAppearing`/`OnDisappearing` and `IDisposable.Dispose` all fire on
  the dialog ViewModel, and with the default modal presenter the page underneath gets `OnDisappearing`
  when the dialog opens and `OnAppearing` when it closes. `INavigationAware`, `INavigationConfirmation`
  and the `Navigating`/`Navigated` events are deliberately NOT involved — a dialog is not a stack mutation.
- The dialog ViewModel is disposed as the page detaches, marginally before `ShowDialog` returns. The
  returned `DialogResult<T>` is unaffected, but do not use the ViewModel instance after the await.

**Changing presentation** — the ViewModel, `IDialogAware<T>` and the call site never change; only the
registered `IDialogPresenter` does.

| Presenter | Package | Registration | Presentation |
|:----------|:--------|:-------------|:-------------|
| `ShellModalDialogPresenter` (default) | `Shiny.Maui.Shell` | — | Page on Shell's modal stack |
| `ShinyOverlayDialogPresenter` | `Shiny.Maui.Shell.ShinyDialogs` | `UseShinyDialogPresenter()` | Themed card over a dimmed backdrop, inside the current page |
| `UxDiversDialogPresenter` | `Shiny.Maui.Shell.UxDiversDialogs` | `UseUxDiversDialogPresenter()` | UXDivers `PopupPage` over a dimmed backdrop |

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseShinyDialogPresenter(o =>        // or .UseUxDiversDialogPresenter(o => ...)
    {
        o.BackdropOpacity = 0.6;         // both: BackdropOpacity, BackdropColor, DismissOnBackdropTap,
        o.CornerRadius = 24;             //       CornerRadius, CardBackgroundColor, MaxWidth, Margin,
        o.MaxWidth = 480;                //       AnimationDuration
    })
);
```

Both overlay presenters take the dialog page's **Content**, so the dialog page must be a `ContentPage`
with content set — they throw otherwise. The page underneath stays on screen behind the scrim, so it
does NOT get `OnDisappearing`/`OnAppearing` around the dialog; the dialog ViewModel's own hooks are
unchanged. A backdrop tap (or, for the overlay presenter, the host page disappearing) reports
`IsCancelled`.

**Writing your own** — implement `IDialogPresenter` for a host that takes a `Page`. Its task must
complete when the page is gone (user-dismissed *or* `dismiss` fired), and must not throw
`OperationCanceledException` on `dismiss`:

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseDialogPresenter<MyPopupPresenter>()
);
```

For a host that takes a `View` (popup / bottom sheet / overlay), derive from `ViewDialogPresenter`
instead of implementing the interface directly — it unwraps the page, sets the binding context, raises
`IPageLifecycleAware`, disposes the ViewModel and restores the content afterwards:

```csharp
public class MySheetPresenter(IMainThread mainThread) : ViewDialogPresenter(mainThread)
{
    // called on the main thread; detach `content` from your host before returning
    protected override async Task PresentView(View content, object viewModel, CancellationToken dismiss)
        => /* show content, complete when it is gone */;
}
```

### 7. ShellServices Aggregate & IMainThread

`ShellServices` is a convenience record that bundles the three shell services together. Inject it when a ViewModel or service needs most of them and you want a single parameter:

```csharp
public record ShellServices(
    INavigator Navigator,
    IDialogs Dialogs,
    IMainThread MainThread
);

public class MyViewModel(ShellServices shell)
{
    async Task DoWork()
    {
        shell.MainThread.BeginInvokeOnMainThread(() => /* UI update */);
        await shell.Dialogs.Alert("Done", "Work complete");
        await shell.Navigator.GoBack();
    }
}
```

`IMainThread` is the thread-marshalling abstraction used internally by `ShellNavigator` and `ShellDialogs`. Prefer it over `Microsoft.Maui.ApplicationModel.MainThread` inside Shiny Shell code because the default implementation (`MauiMainThread`) transparently works around platforms where MAUI's `MainThread.InvokeOnMainThreadAsync` is broken — currently macOS and Linux, where calls are executed inline instead of being dispatched.

```csharp
public interface IMainThread
{
    Task InvokeOnMainThreadAsync(Action action);
    Task InvokeOnMainThreadAsync(Func<Task> func);
    Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> func);
    void BeginInvokeOnMainThread(Action action);
}
```

Both `ShellServices` and `IMainThread` are registered as singletons by `UseShinyShell()` — no extra setup required.

### 8. Modal Pages

Set `Shell.PresentationMode="Modal"` on the page XAML:
```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             Shell.PresentationMode="Modal"
             x:Class="MyApp.ModalPage">
```

Navigate to it like any other page. Close with `GoBack()`.

If the modal exists to collect a value from the user, prefer §6a instead — `ShowDialog` presents it
modally for you and hands back a typed result.

### 9. File Organization

Place files following standard MAUI conventions:
- Pages: `Views/{Name}Page.xaml` + `Views/{Name}Page.xaml.cs`
- ViewModels: `ViewModels/{Name}ViewModel.cs`
- Or co-locate: `Features/{Feature}/{Name}Page.xaml` + `{Name}ViewModel.cs`

## Source Generation Output

The source generator produces several files from `[ShellMap]` and `[ShellProperty]` attributes. Each can be individually disabled via MSBuild properties.

### Routes.g.cs
The constant name is derived from the `route` parameter (or page type name without `Page` suffix when no route is specified):
```csharp
public static class Routes
{
    public const string Detail = "Detail";
    public const string Settings = "Settings";
}
```

### NavigationExtensions.g.cs
Method names are also derived from the route parameter:
```csharp
public static class NavigationExtensions
{
    public static Task NavigateToDetail(this INavigator navigator, string itemId, int pageIndex = default)
    {
        return navigator.NavigateTo<DetailViewModel>(x =>
        {
            x.ItemId = itemId;
            x.PageIndex = pageIndex;
        });
    }
}
```

### DialogExtensions.g.cs
Only emitted for `[ShellMap]` ViewModels that also implement `IDialogAware<T>`. Not generated at all when there are none, and never added to the AI tool surface:
```csharp
public static class DialogExtensions
{
    public static Task<DialogResult<string>> ShowPickColorDialog(this INavigator navigator,
        string preset = null,
        CancellationToken cancellationToken = default)
    {
        return navigator.ShowDialog<PickColorViewModel, string>(x => { x.Preset = preset; }, cancellationToken);
    }
}
```

### NavigationBuilderExtensions.g.cs
Uses inline string literals (not `Routes.*` constants), so it works regardless of whether route constants are enabled:
```csharp
public static class NavigationBuilderExtensions
{
    public static ShinyAppBuilder AddGeneratedMaps(this ShinyAppBuilder builder)
    {
        builder.Add<DetailPage, DetailViewModel>("Detail");
        builder.Add<SettingsPage, SettingsViewModel>("Settings");
        return builder;
    }
}
```

### AppLinkExtensions.g.cs

Generated only when `appLinks` templates exist **and** exactly one scheme (or one domain, with no
scheme) is configured - otherwise there is no single correct base URL to build against.

```csharp
var uri = navigator.CreateProductAppLink(id: 42, tab: "reviews");
// myapp://product/42?Tab=reviews
```

App link registrations are also appended to `AddGeneratedMaps()` as `builder.AddAppLink<TViewModel>(...)`
calls carrying a source-generated binder - never call `AddAppLink` by hand.

### Configuring Source Generation

Disable individual generated files via MSBuild properties in `.csproj`:

```xml
<PropertyGroup>
    <!-- Disable Routes.g.cs -->
    <ShinyMauiShell_GenerateRouteConstants>false</ShinyMauiShell_GenerateRouteConstants>

    <!-- Disable NavigationExtensions.g.cs and DialogExtensions.g.cs -->
    <ShinyMauiShell_GenerateNavExtensions>false</ShinyMauiShell_GenerateNavExtensions>

    <!-- Disable AI extensions (enabled by default, requires Microsoft.Extensions.AI) -->
    <ShinyMauiShell_GenerateAiExtensions>false</ShinyMauiShell_GenerateAiExtensions>

    <!-- Customize the generated AI tools class name (default: AiMauiShellTools) -->
    <ShinyMauiShell_AiToolsClassName>MyAppAiTools</ShinyMauiShell_AiToolsClassName>

    <!-- Customize the generated static extensions class name (default: AiExtensions) -->
    <ShinyMauiShell_AiExtensionsClassName>MyAppRouteExtensions</ShinyMauiShell_AiExtensionsClassName>

    <!-- Customize the AI navigate method name (default: NavigateToRoute) -->
    <ShinyMauiShell_AiNavigateMethodName>GoToPage</ShinyMauiShell_AiNavigateMethodName>
</PropertyGroup>
```

| Property | Default | Controls |
|---|---|---|
| `ShinyMauiShell_GenerateRouteConstants` | `true` | `Routes.g.cs` |
| `ShinyMauiShell_GenerateNavExtensions` | `true` | All navigation extensions and `AddGeneratedMaps` |
| `ShinyMauiShell_GenerateAiExtensions` | `true` | `AiMauiShellTools` class, `AddAiTools()`, `GetAiToolApplicableGeneratedRoutes`, `NavigateToRoute`, and `Prompt`. Requires `Microsoft.Extensions.AI` (**SHINY003** error if missing). Set to `false` to disable |
| `ShinyMauiShell_AiToolsClassName` | `AiMauiShellTools` | Class name for the generated AI tools class |
| `ShinyMauiShell_AiExtensionsClassName` | `AiExtensions` | Class name for the static route info extensions class |
| `ShinyMauiShell_AiNavigateMethodName` | `NavigateToRoute` | Method name for the AI-friendly navigate method |

## Complete ViewModel Example

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace MyApp.ViewModels;

[ShellMap<DetailPage>("Detail")]
public partial class DetailViewModel(INavigator navigator, IDialogs dialogs) : ObservableObject,
    IPageLifecycleAware,
    INavigationConfirmation,
    INavigationAware,
    IDisposable
{
    [ShellProperty]
    [ObservableProperty]
    string itemId;

    [ObservableProperty]
    string title;

    bool hasUnsavedChanges;

    // Page appeared
    public void OnAppearing()
    {
        // Load data, start listening, etc.
    }

    // Page disappearing
    public void OnDisappearing()
    {
        // Pause operations
    }

    // Confirm before leaving
    public async Task<bool> CanNavigate()
    {
        if (!hasUnsavedChanges)
            return true;

        return await dialogs.Confirm(
            "Unsaved Changes",
            "You have unsaved changes. Discard them?"
        );
    }

    // Mutate parameters before leaving
    public void OnNavigatingFrom(IDictionary<string, object> parameters)
    {
        parameters["LastViewedItem"] = ItemId;
    }

    [RelayCommand]
    async Task Save()
    {
        // Save logic
        hasUnsavedChanges = false;
        await navigator.GoBack(("Saved", true));
    }

    [RelayCommand]
    Task GoBack() => navigator.GoBack();

    public void Dispose()
    {
        // Cleanup subscriptions, timers, etc.
    }
}
```

## Best Practices

1. **Use source generation** - Always prefer `[ShellMap]` + `[ShellProperty]` + `AddGeneratedMaps()` over manual registration
2. **Inject INavigator** - Never use `Shell.Current.GoToAsync` directly; use `INavigator` for testability
3. **Inject IDialogs** - Never use `Shell.Current.DisplayAlert` directly; use `IDialogs` for testability
4. **Use primary constructors** - Inject dependencies via primary constructor parameters
5. **Use `[ShellProperty]`** - Properties are set directly by generated navigation methods — no `IQueryAttributable` needed
6. **Use ObservableObject** - From CommunityToolkit.Mvvm as the ViewModel base class
7. **Implement IDisposable** - Clean up event handlers and subscriptions to prevent memory leaks
8. **Use CanNavigate for guards** - Protect unsaved changes with `INavigationConfirmation`; use `INavigationInterceptor` when the rule is app-wide or needs to redirect (auth, feature flags)
9. **Mark ViewModel partial** - Required when using `[ShellMap]` source generation and CommunityToolkit attributes
10. **Pass results via GoBack args** - Return data to the previous page through navigation parameters
11. **Use tab badges only on shell tabs** - Badge APIs resolve existing tab routes in the active Shell
12. **Use `Navigate.*` for lightweight XAML wiring** - Prefer ViewModel commands when navigation needs branching logic or validation

## Reference Files

For detailed templates and examples, see:
- `reference/templates.md` - Page and ViewModel code generation templates
- `reference/api-reference.md` - Full API surface, interfaces, and attributes

## Common Packages

```bash
dotnet add package Shiny.Maui.Shell           # Core library with source generators
dotnet add package CommunityToolkit.Mvvm      # ObservableObject, RelayCommand, etc.
```
