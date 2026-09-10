# Shiny MAUI Shell

[![NuGet](https://img.shields.io/nuget/v/Shiny.Maui.Shell?style=for-the-badge)](https://www.nuget.org/packages/Shiny.Maui.Shell)

Make .NET MAUI Shell shinier with ViewModel lifecycle management, navigation services, and source generation to remove boilerplate, reduce errors, and make your app testable.

Inspired by [Prism Library](https://prismlibrary.com) by Dan Siegel and Brian Lagunas.

[Full Documentation](https://shinylib.net/maui)

---

## Features

### 🧭 Navigation — `INavigator`

| Capability | Description |
|:-----------|:------------|
| Route-based | `NavigateTo("Detail", args: [("Id", "123")])` |
| ViewModel-based | `NavigateTo<DetailViewModel>(vm => vm.Id = "123")` |
| Source-generated | `NavigateToDetail("123")` — zero guesswork |
| GoBack | Single page, multi-page `GoBack(3)`, or `PopToRoot()` |
| Root navigation | `NavigateTo<DashboardViewModel>(relativeNavigation: false)` — reset the stack |
| Navigation builder | Fluent multi-segment: `CreateBuilder().AddDetail(42).AddModal().Navigate()` |
| Shell switching | `SwitchShell(new MainShell())` or `SwitchShell<TShell>()` via DI |
| Tab badges | Numeric tab badges via route or ViewModel — `SetTabBadge<InboxViewModel>(3)` |
| XAML navigation | Attached properties on `Button`, `MenuItem`, and `ToolbarItem` |

### 🛡️ Navigation Interceptors — `INavigationInterceptor`

| Capability | Description |
|:-----------|:------------|
| Guard every navigation | Routes, ViewModels, builder, back, app links, shortcuts, tab taps |
| Cancel | `NavigationInterceptorResult.Cancel()` — the user stays put |
| Redirect | `Redirect("//Login")` or refactor-safe `Redirect<LoginViewModel>()` |
| Destination ViewModel | Resolved and populated *before* the interceptor runs, so guards can read it |
| Chained | Multiple interceptors run in `Order` then registration order; a redirect re-runs the chain |
| Bypassable | `NavigateTo(..., bypassInterceptors: true)` for the navigation a guard itself performs |
| Answerable | Every navigation method returns `Task<bool>` - false when a guard cancelled it |

### 💬 Dialogs — `IDialogs`

| Method | Returns |
|:-------|:--------|
| `Alert(title, message)` | `Task` |
| `Confirm(title, message)` | `Task<bool>` |
| `Prompt(title, message)` | `Task<string?>` |
| `ActionSheet(title, cancel, destructive, ...buttons)` | `Task<string>` |

For anything richer than the four primitives above, present one of your own pages as a dialog and
`await` a typed result from it with `INavigator.ShowDialog<TViewModel, T>` — see
[ViewModel Dialogs](#8-viewmodel-dialogs).

> Thread-safe — dispatches to UI thread automatically. Inject separately from `INavigator` for clean separation of concerns.
>
> **Alternative providers (same `IDialogs` interface, no ViewModel changes needed):**
> - `Shiny.Maui.Shell.ShinyDialogs` — owned, animated, themeable dialogs powered by [Shiny.Maui.Controls](https://shinylib.net/controls/dialogs/) (never the native alert/prompt; identical across platforms).
> - `Shiny.Maui.Shell.UxDiversDialogs` — styled popup dialogs powered by [UXDivers Popups](https://github.com/uxdivers/uxd-popups).
>
> Both packages also ship an `IDialogPresenter`, so your *ViewModel* dialogs render as a card over a
> dimmed backdrop instead of a modal page — see [Changing how dialogs appear](#changing-how-dialogs-appear).

### 📡 Navigation Events

| Event | Fires | Key Properties |
|:------|:------|:---------------|
| `Navigating` | Before navigation | `FromUri` · `FromViewModel` · `ToUri` · `NavigationType` · `Parameters` |
| `Navigated` | After page resolves | `ToUri` · `ToViewModel` · `NavigationType` · `Parameters` |

`NavigationType`: `Push` · `SetRoot` · `GoBack` · `PopToRoot` · `SwitchShell`

### ♻️ ViewModel Lifecycle

| Interface | Method | Purpose |
|:----------|:-------|:--------|
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` | Page visibility hooks |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` | Guard leaving the page - user-driven navigation only (tab tap, flyout, hardware back) |
| `INavigationAware` | `OnNavigatingFrom(params)` | Mutate parameters before leaving |
| `IQueryAttributable` | `ApplyQueryAttributes(params)` | Receive navigation parameters (only for string-based `NavigateTo` — not needed with `[ShellProperty]`) |
| `IDisposable` | `Dispose()` | Cleanup when page leaves the stack |

### ⚡ Source Generation

| Generated File | What It Does |
|:----------------|:------------|
| `Routes.g.cs` | Static route constants — `Routes.Detail` |
| `NavigationExtensions.g.cs` | Typed methods — `NavigateToDetail(id, page)` with XML docs and `[Description]` attributes |
| `NavigationBuilderNavExtensions.g.cs` | Typed builder methods — `AddDetail(id, page)` |
| `NavigationBuilderExtensions.g.cs` | One-line DI — `AddGeneratedMaps()` |
| `AiExtensions.g.cs` | Route metadata — `GetGeneratedRouteInfo()`, plus `AiMauiShellTools` class with `Prompt`, `Tools`, `GetAiToolApplicableGeneratedRoutes()`, and `NavigateToRoute()` when AI extensions are enabled. Also generates `AddAiTools()` extension on `ShinyAppBuilder` for DI registration |

> Invalid route names produce **SHINY001** compiler errors. Disable individual outputs via MSBuild properties.

### 🔌 Custom Handlers

| Handler | Description |
|:--------|:------------|
| `DisableShellFlyoutSwipeHandler` | Disables the flyout swipe gesture while keeping the hamburger button functional. Opt-in via `DisableShellFlyoutSwipeHandler.Register()` |

### ✅ Zero Ceremony

- One base class change — `AppShell : ShinyShell` — for deterministic BindingContext assignment
- Page–ViewModel mapping with **automatic BindingContext** assignment
- Drop-in `[ShellMap]` attribute replaces manual route registration

---

## Getting Started

### 1. Install

```bash
dotnet add package Shiny.Maui.Shell
```

### 2. Configure MauiProgram.cs

**With source generation (recommended):**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x.AddGeneratedMaps());
```

**Manual registration:**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .Add<MainPage, MainViewModel>(registerRoute: false) // pages in AppShell.xaml
        .Add<DetailPage, DetailViewModel>("Detail")
        .Add<SettingsPage, SettingsViewModel>("Settings")
    );
```

### 3. Set up AppShell

Your `AppShell` must inherit from `ShinyShell` instead of `Shell`:

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

> [!NOTE]
> Pages defined in AppShell.xaml should use `registerRoute: false`.

### 4. Navigate

Inject `INavigator` into your ViewModels:

```csharp
public class MyViewModel(INavigator navigator)
{
    // Route-based navigation with args
    await navigator.NavigateTo("Detail", args: [("ItemId", "123")]);

    // ViewModel-based navigation with strongly-typed configuration
    await navigator.NavigateTo<DetailViewModel>(vm => vm.ItemId = "123");

    // Source-generated strongly-typed method (preferred)
    await navigator.NavigateToDetail("123");

    // Root navigation — resets the stack
    await navigator.NavigateTo<DashboardViewModel>(relativeNavigation: false);

    // Go back with result
    await navigator.GoBack(("Result", selectedItem));

    // Go back multiple pages
    await navigator.GoBack(2);

    // Pop to root
    await navigator.PopToRoot();

    // Switch to a different Shell instance
    await navigator.SwitchShell(new MainAppShell());

    // Switch to a Shell resolved from DI
    await navigator.SwitchShell<MainAppShell>();

    // Set or clear a numeric badge on a tab in the active Shell
    await navigator.SetTabBadge("Inbox", 3);
    await navigator.SetTabBadge<InboxViewModel>(7);
    await navigator.ClearTabBadge("Inbox");
    await navigator.ClearTabBadge<InboxViewModel>();

    // Fluent multi-segment navigation builder
    await navigator
        .CreateBuilder()
        .AddDetail(id: 42)
        .AddModal()
        .Navigate();

    // Pop back 2 pages, then push
    await navigator
        .CreateBuilder()
        .PopBack(2)
        .AddHome()
        .Navigate();

    // Navigate from root with builder
    await navigator
        .CreateBuilder(fromRoot: true)
        .AddDashboard()
        .AddDetail(id: 1)
        .Navigate();
}
```

> [!IMPORTANT]
> Root navigation (`relativeNavigation: false` or `CreateBuilder(fromRoot: true)`) uses the `//` URI prefix, which requires the target route to be declared in your `AppShell.xaml`. Routes registered only via `Routing.RegisterRoute` or `[ShellMap]` cannot be navigated to from root. Add the page as a `ShellContent` in your Shell XAML and use `registerRoute: false` in `[ShellMap]`.

> [!NOTE]
> If you're setting arguments on the ViewModel navigation, you should make them observable if they are bound on the Page.

> [!IMPORTANT]
> Tab badges only work for routes that are already present as tabs in the active Shell. The badge APIs are supported on Android, iOS, Mac Catalyst, and Windows. Linux and macOS AppKit throw `PlatformNotSupportedException`.

### 4.1 XAML Navigation

Use `Navigate` attached properties when you want route-based navigation directly from XAML without a ViewModel command:

```xml
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell">

    <Button Text="Open Detail"
            shiny:Navigate.Route="Detail"
            shiny:Navigate.ParameterKey="ItemId"
            shiny:Navigate.ParameterValue="{Binding SelectedId}" />

    <ToolbarItem Text="Home"
                 shiny:Navigate.Route="MainPage"
                 shiny:Navigate.RelativeNavigation="False" />
</ContentPage>
```

For multiple parameters:

```xml
<Button Text="Open Modal"
        shiny:Navigate.Route="modal">
    <shiny:Navigate.Parameters>
        <shiny:NavigationParameters>
            <shiny:NavigationParameter Key="Arg1" Value="{Binding NavArg}" />
            <shiny:NavigationParameter Key="Arg2" Value="5" />
        </shiny:NavigationParameters>
    </shiny:Navigate.Parameters>
</Button>
```

`Navigate` currently supports `Button`, `MenuItem`, and `ToolbarItem`.

### 5. Dialogs

Inject `IDialogs` for user-facing dialogs:

```csharp
public class MyViewModel(IDialogs dialogs)
{
    // Alert
    await dialogs.Alert("Error", "Something went wrong");

    // Confirm
    if (await dialogs.Confirm("Delete?", "Are you sure?"))
    {
        // delete
    }

    // Prompt for text input
    var name = await dialogs.Prompt("Name", "Enter your name", placeholder: "John Doe");
    if (name != null)
    {
        // user entered a value
    }

    // Action sheet
    var choice = await dialogs.ActionSheet("Options", "Cancel", "Delete", "Edit", "Share");
}
```

### 6. Shiny Controls Dialogs (Optional)

Replace the default native dialogs with the owned, animated, themeable dialog service from [Shiny.Maui.Controls](https://shinylib.net/controls/dialogs/). The dialog is always rendered by the library, so it looks identical across platforms:

```bash
dotnet add package Shiny.Maui.Shell.ShinyDialogs
```

Configure in `MauiProgram.cs` — `UseShinyControls()` registers the underlying `IDialogService`, and `UseShinyDialogs()` routes `IDialogs` through it:
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyControls()             // registers the Shiny.Maui.Controls IDialogService
    .UseShinyShell(x => x
        .UseShinyDialogs()          // route IDialogs through the Controls dialog service
        .UseShinyDialogPresenter()  // ...and render ViewModel dialogs as an overlay card
        .AddGeneratedMaps()
    )
```

Your ViewModels continue using `IDialogs` as before — only the visual presentation changes.

`UseShinyDialogPresenter()` is the matching half for [ViewModel dialogs](#8-viewmodel-dialogs): instead
of pushing the dialog page onto Shell's modal stack, it floats the page's content as a themed card
over a dimmed backdrop, using the active Shiny theme's `Surface` and `Scrim` colours. On a
`ShinyContentPage` the overlay goes into that page's own `OverlayHost`; on a plain `ContentPage` it is
layered over the content.

```csharp
.UseShinyDialogPresenter(o =>
{
    o.BackdropOpacity = 0.6;
    o.CornerRadius = 24;
    o.MaxWidth = 480;
    o.DismissOnBackdropTap = false;     // dialog closes only via the ViewModel's events
})
```

### 7. UxDivers Dialogs (Optional)

Replace the default platform dialogs with styled popups from [UXDivers Popups](https://github.com/uxdivers/uxd-popups):

```bash
dotnet add package UXDivers.Popups.Maui
```

Add theme dictionaries to `App.xaml`:
```xml
<ResourceDictionary.MergedDictionaries>
    <!-- your existing styles -->
    <uxd:DarkTheme xmlns:uxd="clr-namespace:UXDivers.Popups.Maui.Controls;assembly=UXDivers.Popups.Maui" />
    <uxd:PopupStyles xmlns:uxd="clr-namespace:UXDivers.Popups.Maui.Controls;assembly=UXDivers.Popups.Maui" />
</ResourceDictionary.MergedDictionaries>
```

Configure in `MauiProgram.cs`:
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .UseUxDiversDialogs()           // route IDialogs through UXDivers popups
        .UseUxDiversDialogPresenter()   // ...and render ViewModel dialogs as a popup
        .AddGeneratedMaps()
    )
```

Either call initializes the UXDivers popup infrastructure (`UseUXDiversPopups()`), and doing both
initializes it once.

Your ViewModels continue using `IDialogs` as before — only the visual presentation changes.

`UseUxDiversDialogPresenter()` is the matching half for [ViewModel dialogs](#8-viewmodel-dialogs): the
dialog page's content is hosted in a UXDivers `PopupPage` — a card over a dimmed backdrop, built the
way their own custom popups are, so it matches the alert/confirm/prompt popups beside it.

```csharp
.UseUxDiversDialogPresenter(o =>
{
    o.BackdropOpacity = 0.6;
    o.CornerRadius = 24;
    o.MaxWidth = 480;
    o.ConfigurePopup = popup => popup.AppearingAnimation = new MoveInPopupAnimation();
})
```

### 8. ViewModel Dialogs

`IDialogs` covers alert / confirm / prompt / action sheet. When you need a *real* UI to collect a
result — a colour picker, a filter sheet, a signature pad — present one of your own Page/ViewModel
pairs as a dialog and `await` the value it produces.

The ViewModel implements `IDialogAware<T>` and raises one of two events to close itself:

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

The source generator emits a typed `Show{Route}Dialog` extension for it, so the call site needs no
type arguments and `[ShellProperty]` values become parameters:

```csharp
var result = await navigator.ShowPickColorDialog(preset: "Violet");

if (result.TryGetValue(out var colour))
    this.Selected = colour;

// or
this.Selected = result.ValueOr("Red");
```

`DialogResult<T>` exists because `default(T)` cannot express cancellation for value types — a `bool`
dialog could not otherwise tell "the user chose No" apart from "the user dismissed it".

| Outcome | Result |
|:--------|:-------|
| ViewModel raised `Completed` | `IsCancelled == false`, `Value` set |
| ViewModel raised `Cancelled` | `IsCancelled == true` |
| User dismissed the dialog (back button, iOS swipe-down) | `IsCancelled == true` |
| The `CancellationToken` you passed fired | throws `OperationCanceledException` |

**Every dismissal path completes the `await`** — a dialog closed without either event being raised
reports cancellation rather than hanging.

The underlying method is available directly if you'd rather not use the generated wrapper, though it
needs both type arguments spelled out (C# cannot infer a type argument from a constraint):

```csharp
var result = await navigator.ShowDialog<PickColorViewModel, string>(x => x.Preset = "Violet");
```

The dialog ViewModel must be mapped to a page like any other navigable ViewModel — via `[ShellMap]`
+ `AddGeneratedMaps()` or `ShinyAppBuilder.Add<TPage, TViewModel>()`.

#### Lifecycle

| Hook | Dialog ViewModel | Page underneath |
|:-----|:-----------------|:----------------|
| `IPageLifecycleAware.OnAppearing` | ✅ when shown | ✅ when the dialog closes |
| `IPageLifecycleAware.OnDisappearing` | ✅ when closed | ✅ when the dialog opens |
| `IDisposable.Dispose` | ✅ when closed | — |
| `INavigationAware.OnNavigatingFrom` | ❌ not raised | ❌ not raised |
| `INavigationConfirmation.CanNavigate` | ❌ not consulted | ❌ not consulted |
| `INavigator.Navigating` / `Navigated` | ❌ not raised | ❌ not raised |

The three that don't fire are deliberate: showing a dialog is not a navigation stack mutation, and an
"are you sure you want to leave?" guard firing because a dialog opened would be wrong.

The dialog ViewModel is disposed as the page detaches, which happens marginally *before* `ShowDialog`
returns. The `DialogResult<T>` you get back is captured at the moment the ViewModel raised its event
and is unaffected — but don't hold on to the ViewModel instance after the await.

#### Changing how dialogs appear

*How* a dialog appears is decided by the registered `IDialogPresenter` — the ViewModel, the
`IDialogAware<T>` contract, and the call site are identical whichever one you pick.

| Presenter | Package | Presentation |
|:----------|:--------|:-------------|
| `ShellModalDialogPresenter` *(default)* | `Shiny.Maui.Shell` | The page on Shell's modal stack |
| `ShinyOverlayDialogPresenter` | `Shiny.Maui.Shell.ShinyDialogs` | A themed card over a dimmed backdrop, in the current page |
| `UxDiversDialogPresenter` | `Shiny.Maui.Shell.UxDiversDialogs` | A UXDivers `PopupPage` over a dimmed backdrop |

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseShinyDialogPresenter()        // or .UseUxDiversDialogPresenter()
);
```

The two overlay presenters keep the page underneath on screen behind the scrim, which changes the
lifecycle table above in one place: the page underneath does **not** disappear when the dialog opens,
so it neither raises `OnDisappearing` nor `OnAppearing`. The dialog ViewModel's own hooks — including
`Dispose` — fire exactly as they do for a modal.

Both are dismissed by a tap on the backdrop (`DismissOnBackdropTap = false` turns that off), and both
report a dismissal as `IsCancelled`. The overlay presenter also treats the host page disappearing as a
dismissal: an overlay lives inside a page, so a navigation away takes the dialog with it, and the
awaiting caller has to be released rather than left hanging.

**Writing your own.** Implement `IDialogPresenter` for anything that can host a `Page`:

```csharp
public class MyPresenter : IDialogPresenter
{
    // Show the page; complete the Task once it is gone, either because the user
    // dismissed it or because `dismiss` fired. Never throw on `dismiss`.
    public async Task Present(Page page, object viewModel, CancellationToken dismiss) { /* ... */ }
}

builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseDialogPresenter<MyPresenter>()
);
```

For a host that takes a `View` rather than a `Page` — a popup, a bottom sheet, a custom overlay —
derive from `ViewDialogPresenter` instead. It hands you the page's content with the binding context
already set, and takes care of what the page would otherwise have done for you: raising
`IPageLifecycleAware`, disposing the ViewModel, and giving the content back to its page afterwards.

```csharp
public class MySheetPresenter(IMainThread mainThread) : ViewDialogPresenter(mainThread)
{
    protected override async Task PresentView(View content, object viewModel, CancellationToken dismiss)
    {
        // Called on the main thread. Show `content`; complete once it is gone, and
        // detach it from your host before returning.
    }
}
```

---

## Navigation Events

Subscribe to `Navigating` and `Navigated` on `INavigator` for cross-cutting concerns like logging or analytics:

```csharp
public class NavigationLogger(
    ILogger<NavigationLogger> logger,
    INavigator navigator
) : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        navigator.Navigating += (_, args) =>
            logger.LogInformation("Navigating from '{From}' to '{To}' ({Type})",
                args.FromUri, args.ToUri, args.NavigationType);

        navigator.Navigated += (_, args) =>
            logger.LogInformation("Navigated to '{To}' - ViewModel: {VM} ({Type})",
                args.ToUri, args.ToViewModel?.GetType().Name, args.NavigationType);
    }
}

// Register in MauiProgram.cs
builder.Services.AddSingleton<IMauiInitializeService, NavigationLogger>();
```

---

## Navigation Interceptors

`INavigationInterceptor` sits in front of every navigation the app makes and can let it through,
cancel it, or send it somewhere else. Register as many as you like - they run in registration order
and the first one to cancel or redirect wins.

```csharp
public interface INavigationInterceptor
{
    Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    );

    // Lowest runs first; ties keep registration order
    int Order => 0;
}
```

Every navigation method returns `Task<bool>` - false means a guard cancelled it:

```csharp
if (!await navigator.NavigateTo<DetailViewModel>())
    // an interceptor said no
```

```csharp
public class AuthNavigationInterceptor(IAuthService auth) : INavigationInterceptor
{
    // Guards run before anything that only observes
    public int Order => -100;

    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        if (await auth.IsAuthorized(cancellationToken) || uri.Contains("Login"))
            return NavigationInterceptorResult.Continue;

        return NavigationInterceptorResult.Redirect<LoginViewModel>();   // or Redirect("//Login")
    }
}
```

```csharp
// MauiProgram.cs
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddNavigationInterceptor<AuthNavigationInterceptor>()
    .AddNavigationInterceptor<AuditNavigationInterceptor>()

    // or inline, for a one-line rule
    .AddNavigationInterceptor((uri, vm, ct) =>
    {
        Console.WriteLine($"Navigating to {uri}");
        return Task.FromResult(NavigationInterceptorResult.Continue);
    }, order: 100)
);
```

Interceptors run in `Order` (lowest first), then registration order. They are singletons, so keep
no per-navigation state in fields.

### What gets intercepted

| Path | Intercepted | `viewModel` argument |
|---|---|---|
| `NavigateTo(route)` | ✅ | Resolved from the route's ViewModel mapping |
| `NavigateTo<TViewModel>(configure)` | ✅ | Your instance, after `configure` ran |
| `CreateBuilder()...Navigate()` | ✅ | The last segment's ViewModel (the page the user lands on) |
| `GoBack` / `PopToRoot` | ✅ | The existing ViewModel from the navigation stack |
| App links & app shortcuts | ✅ | The ViewModel with the link's values already applied |
| Tab taps, flyout items, hardware back | ✅ | `null` - Shell builds these, so Shiny doesn't construct one |
| `ShowDialog` / `SwitchShell` | ❌ | Not navigation |

The ViewModel handed over is the **destination** one, resolved and fully populated *before* the
interceptor runs - so a guard can decide on the destination's own state, not just its URI, and any
change it makes sticks: that instance is what gets bound to the page. (Navigation arguments are
applied by Shell afterwards, so they win over an interceptor's edits to the same property, and a
route declared as a `ShellContent` in AppShell XAML keeps the ViewModel its page was already bound
to - the interceptor sees a resolved instance, but changes to it do not reach an on-screen page.)

The page being *left* comes from `INavigationContextAccessor`, along with the rest of the
navigation:

```csharp
public class UnsavedChangesNavigationInterceptor(
    INavigationContextAccessor context,
    IDialogs dialogs
) : INavigationInterceptor
{
    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(string uri, object? viewModel)
    {
        if (context.Current?.FromViewModel is not IUnsavedChanges { HasUnsavedChanges: true })
            return NavigationInterceptorResult.Continue;

        return await dialogs.Confirm("Unsaved Changes", "Discard changes?")
            ? NavigationInterceptorResult.Continue
            : NavigationInterceptorResult.Cancel();
    }
}
```

`NavigationContext` carries `FromUri`, `FromViewModel`, `ToUri`, `NavigationType`, `Direction`,
`Parameters` and `RedirectCount`. It is only set while an interceptor is running.

`Direction` is the coarse question `NavigationType` answers precisely - `Forward` (push), `Back`
(`GoBack`, `PopToRoot`) or `Root` (absolute route, Shell swap). It is on `NavigationEventArgs` and
`NavigatedEventArgs` too, and any `NavigationType` converts with `.GetDirection()`.

### The escape hatch

A guard that navigates would otherwise guard itself. Every navigation method takes
`bypassInterceptors`:

```csharp
await navigator.NavigateTo<LoginViewModel>(bypassInterceptors: true);
await navigator.GoBack(1, bypassInterceptors: true);
await navigator.PopToRoot(bypassInterceptors: true);
await navigator.CreateBuilder().AddDetail(42).Navigate(bypassInterceptors: true);

// the builder is fluent, so it reads fluently too
await navigator.CreateBuilder().BypassInterceptors().AddDetail(42).Navigate();
```

A `RedirectUri` never needs it - the chain restarting on a redirect is deliberate, and a redirect
to the destination already being navigated to is ignored rather than looping.

Interceptors also receive the `CancellationToken` passed to the navigation call - for the network
call an auth guard makes, not for the decision itself. Cancelling it abandons the navigation with
an `OperationCanceledException`.

### Cancel and redirect

| Result | Behaviour |
|---|---|
| `NavigationInterceptorResult.Continue` | Next interceptor, then navigate |
| `Cancel()` | Nothing navigates, the rest of the chain is skipped, the caller's `Task` completes normally |
| `Redirect("Detail")` | Pushes |
| `Redirect("//Main/Home")` | Resets the Shell stack |
| `Redirect("/Login")` | Same as `//Login` - a single leading slash is promoted |
| `Redirect<LoginViewModel>()` | Resets the stack to that ViewModel's route (refactor-safe) |
| `Redirect<DetailViewModel>(relativeNavigation: true)` | Pushes that ViewModel's route |

A redirect **restarts the whole chain** against the new URI, so the redirect target is guarded as
thoroughly as the original destination - and the abandoned destination's ViewModel is dropped
rather than bound to anything. Redirecting to the URI already being navigated to is ignored (an
unconditional "go to login" guard says this every time the user navigates to login); a genuine
redirect loop throws after 10 hops rather than hanging.

An exception thrown from an interceptor propagates to the caller and the navigation does not
happen - a guard that fails is never treated as a guard that passed. On tab taps and hardware back,
where there is no caller, the exception is logged and the navigation is cancelled.

A blocked app link reports `AppLinkResult.Blocked` from `IAppLinks.Handle` - distinct from
`Unhandled` (nothing matched), because the platform hooks still report a blocked link as handled:
saying otherwise invites iOS to open the URL in a browser instead, which is the opposite of what a
guard that just blocked it wants.

### Asking the user

The interceptor is async, so a dialog is a legal thing to await - the navigation has not been
handed to Shell yet and simply waits on the answer. One action sheet can produce all three
outcomes (this is `AskFirstNavigationInterceptor`, on the sample's **Route Interceptor** page):

```csharp
public class AskFirstNavigationInterceptor(
    IDialogs dialogs,
    INavigationContextAccessor context
) : INavigationInterceptor
{
    const string LetItGo = "Let it through";
    const string SendElsewhere = "Redirect to Lifecycle";
    const string StopIt = "Stop navigation";

    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        // narrow to one destination - otherwise every tab tap and back press prompts
        if (viewModel is not DetailViewModel detail)
            return NavigationInterceptorResult.Continue;

        var choice = await dialogs.ActionSheet(
            $"{context.Current?.FromUri} -> {uri} (Text: '{detail.Text}')",
            cancel: StopIt,          // dismissing the sheet lands here too
            destruction: null,
            buttons: [LetItGo, SendElsewhere]
        );

        return choice switch
        {
            LetItGo => NavigationInterceptorResult.Continue,
            SendElsewhere => NavigationInterceptorResult.Redirect<LifecycleDemoViewModel>(relativeNavigation: true),
            _ => NavigationInterceptorResult.Cancel()
        };
    }
}
```

Two things worth copying from it: the early `Continue` when the destination is not the one being
guarded (an interceptor sees *every* navigation, so an unguarded prompt fires on tab taps), and
treating the sheet's cancel/dismiss path as `Cancel()` - the safe default when the user did not
actually answer.

### Interceptors vs. the other hooks

- **`INavigationInterceptor`** - app-wide, about the destination, can cancel *and* redirect.
- **`INavigationConfirmation`** - implemented by the ViewModel being left, answers "may I leave?",
  and only applies to user-driven Shell navigation (tab tap, flyout item, hardware back button):
  `INavigator` calls, app links and shortcuts do not consult it. Asked before the interceptors, and
  unaffected by `bypassInterceptors`, which is about the interceptor chain only.
- **`Navigating` / `Navigated` events** - observation only, they cannot change the outcome.

---

## ViewModel Lifecycle

Implement these interfaces on your ViewModels as needed. Works just like [Prism Library](https://prismlibrary.com).

```csharp
[ShellMap<DetailPage>("Detail", description: "Navigate to the detail page")]
public partial class DetailViewModel(INavigator navigator, IDialogs dialogs) : ObservableObject,
    IPageLifecycleAware,
    INavigationConfirmation,
    IDisposable
{
    [ShellProperty("The item identifier")]
    [ObservableProperty]
    string itemId;

    public void OnAppearing() { /* load data */ }
    public void OnDisappearing() { /* pause */ }

    // Asked when the user navigates away themselves - a tab tap, a flyout item, the hardware
    // back button. Programmatic navigation does not consult it; guard that with an
    // INavigationInterceptor (see Navigation Interceptors above).
    public async Task<bool> CanNavigate()
    {
        if (!hasUnsavedChanges) return true;
        return await dialogs.Confirm("Unsaved Changes", "Discard changes?");
    }

    public void Dispose() { /* cleanup */ }
}
```

---

## Source Generation

Decorate your ViewModels with `[ShellMap]` and `[ShellProperty]` to eliminate boilerplate:

**Input:**
```csharp
[ShellMap<DetailPage>("Detail", description: "Navigate to the detail page")]
public partial class DetailViewModel : ObservableObject
{
    [ShellProperty("The item identifier")]
    public string ItemId { get; set; }

    [ShellProperty("Page number for pagination", required: false)]
    public int Page { get; set; }
}
```

**Generated output:**

```csharp
// Routes.g.cs — constant name matches the route parameter
public static class Routes
{
    public const string Detail = "Detail";
}

// NavigationExtensions.g.cs — typed INavigator methods with XML docs and [Description] attributes
public static class NavigationExtensions
{
    /// <summary>
    /// Navigate to the detail page
    /// </summary>
    /// <param name="itemId">The item identifier</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="relativeNavigation">If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route</param>
    [Description("Navigate to the detail page")]
    public static Task NavigateToDetail(this INavigator navigator,
        [Description("The item identifier")] string itemId,
        [Description("Page number for pagination")] int page = default,
        [Description("If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route")] bool relativeNavigation = true)
    {
        return navigator.NavigateTo<DetailViewModel>(x =>
        {
            x.ItemId = itemId;
            x.Page = page;
        }, relativeNavigation);
    }
}

// NavigationBuilderNavExtensions.g.cs — typed INavigationBuilder methods
public static class NavigationBuilderNavExtensions
{
    public static INavigationBuilder AddDetail(this INavigationBuilder builder,
        string itemId, int page = default)
    {
        return builder.Add<DetailViewModel>(x => { x.ItemId = itemId; x.Page = page; });
    }
}

// NavigationBuilderExtensions.g.cs — uses string literals (not Routes.*)
public static class NavigationBuilderExtensions
{
    public static ShinyAppBuilder AddGeneratedMaps(this ShinyAppBuilder builder)
    {
        builder.Add<DetailPage, DetailViewModel>("Detail");
        return builder;
    }
}

// DialogExtensions.g.cs — only for ViewModels that also implement IDialogAware<T>
public static class DialogExtensions
{
    /// <summary>Navigate to the detail page</summary>
    /// <param name="itemId">The item identifier</param>
    /// <param name="cancellationToken">Dismisses the dialog and throws OperationCanceledException. Distinct from the user cancelling, which returns a cancelled DialogResult.</param>
    [Description("Navigate to the detail page")]
    public static Task<DialogResult<string>> ShowDetailDialog(this INavigator navigator,
        [Description("The item identifier")] string itemId,
        [Description("Page number for pagination")] int page = default,
        CancellationToken cancellationToken = default)
    {
        return navigator.ShowDialog<DetailViewModel, string>(x =>
        {
            x.ItemId = itemId;
            x.Page = page;
        }, cancellationToken);
    }
}

// AiExtensions.g.cs — route metadata (always generated)
public static class AiExtensions
{
    [Description("This provides a list of routes throughout the application")]
    public static GeneratedRouteInfo[] GetGeneratedRouteInfo(this INavigator navigator) =>
    [
        new("Detail", "Navigate to the detail page",
            [new("ItemId", "The item identifier", "string", true),
             new("Page", "Page number for pagination", "int", false)])
    ];
}

// --- AI class and DI extension below generated when AI extensions are enabled ---
// (enabled by default when Microsoft.Extensions.AI is referenced)

// AiMauiShellTools — inject via DI for AI-powered navigation
public class AiMauiShellTools
{
    public AiMauiShellTools(INavigator navigator) { ... }

    // Pre-formatted prompt describing all AI-applicable routes
    public string Prompt { get; }

    // Ready-to-use AITool[] for route discovery and navigation
    public AITool[] Tools { get; }

    // Filtered routes with descriptions and parameters
    public GeneratedRouteInfo[] GetAiToolApplicableGeneratedRoutes() => ...;

    // AI-friendly navigation with automatic type conversion
    public Task<string> NavigateToRoute(string route, Dictionary<string, string>? args = null) { ... }
}

// DI registration extension
public static class AiMauiShellToolsExtensions
{
    public static ShinyAppBuilder AddAiTools(this ShinyAppBuilder builder) { ... }
}
```

Then use it:
```csharp
// MauiProgram.cs - one line to register everything, including AI tools
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddAiTools()          // registers AiMauiShellTools as singleton
);

// Navigate with generated extension methods - no guesswork
await navigator.NavigateToDetail("123", page: 2);

// Present a dialog-aware ViewModel and await its typed result - no type arguments needed
var result = await navigator.ShowPickColorDialog(preset: "Violet");

// Fluent builder with generated extensions
await navigator.CreateBuilder().AddDetail("123", page: 2).Navigate();

// Get route metadata for tooling
var routes = navigator.GetGeneratedRouteInfo();

// AI integration — inject AiMauiShellTools via DI
public class ChatViewModel(AiMauiShellTools aiTools)
{
    var options = new ChatOptions { Tools = [.. aiTools.Tools] };
    // aiTools.Prompt contains the pre-formatted route prompt
}
```

### Route Naming

The `route` parameter in `[ShellMap]` drives the generated constant and method names. It must be a valid C# identifier — invalid names produce a **SHINY001** compiler error.

```csharp
// Route drives the constant and method name
[ShellMap<HomePage>("Dashboard")]
// → Routes.Dashboard = "Dashboard"
// → NavigateToDashboard(...)

// No route — falls back to page type name without "Page" suffix
[ShellMap<HomePage>]
// → Routes.Home = "HomePage"
// → NavigateToHome(...)
```

### Configuring Source Generation

Disable individual generated files via MSBuild properties:

```xml
<PropertyGroup>
    <!-- Disable Routes.g.cs -->
    <ShinyMauiShell_GenerateRouteConstants>false</ShinyMauiShell_GenerateRouteConstants>

    <!-- Disable NavigationExtensions.g.cs, NavigationBuilderNavExtensions.g.cs, DialogExtensions.g.cs, and NavigationBuilderExtensions.g.cs (AddGeneratedMaps) -->
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
| `ShinyMauiShell_GenerateNavExtensions` | `true` | All navigation extensions, `DialogExtensions.g.cs`, and `AddGeneratedMaps` |
| `ShinyMauiShell_GenerateAiExtensions` | `true` | `AiMauiShellTools` class, `AddAiTools()`, `GetAiToolApplicableGeneratedRoutes`, `NavigateToRoute`, and `Prompt`. Requires `Microsoft.Extensions.AI` package (**SHINY003** error if missing). Set to `false` to disable |
| `ShinyMauiShell_AiToolsClassName` | `AiMauiShellTools` | Class name for the generated AI tools class |
| `ShinyMauiShell_AiExtensionsClassName` | `AiExtensions` | Class name for the static route info extensions class |
| `ShinyMauiShell_AiNavigateMethodName` | `NavigateToRoute` | Method name for the AI-friendly navigate method |
| `ShinyAppLinkSchemes` | _(none)_ | Semicolon-separated custom URL schemes for [App Links](#app-links) |
| `ShinyAppLinkDomains` | _(none)_ | Semicolon-separated universal/app link domains |
| `ShinyAppLinkValidation` | `true` | Set to `false` to silence the manifest validation warnings |

`NavigationBuilderExtensions.g.cs` (`AddGeneratedMaps()`) is only generated when `[ShellMap]` attributes are detected and `ShinyMauiShell_GenerateNavExtensions` is not set to `false`. A **SHINY002** warning is emitted if maps are detected but nav extensions are disabled.

`DialogExtensions.g.cs` is only generated when at least one `[ShellMap]` ViewModel also implements `IDialogAware<T>`. Dialog methods are deliberately excluded from the AI tool surface — an AI agent should be driving navigation, not blocking on a modal awaiting human input.

---

## App Links

Deep links are declared where the route already is — the `appLinks` argument of `[ShellMap]`:

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

`myapp://product/123?tab=reviews` and `https://shinylib.net/p/123` both open `ProductPage` with
`Id = 123` and `Tab = "reviews"`.

- `{token}` path segments bind to the `[ShellProperty]` of the same name (case-insensitive).
- Query string values bind by property name too. A path token wins over a query value of the same name.
- Any configured scheme or domain serves any template — adding a domain later needs no attribute change.
- Values are converted with `InvariantCulture`, so `1.5` parses the same on every device.
- A missing or unparseable **required** value is a routing miss, not a crash: the next-best template
  is tried, then `OnUnhandled`.

### Setup

```xml
<PropertyGroup>
  <ShinyAppLinkSchemes>myapp</ShinyAppLinkSchemes>
  <ShinyAppLinkDomains>shinylib.net;www.shinylib.net</ShinyAppLinkDomains>
</PropertyGroup>
```

```csharp
.UseShinyShell(x => x.AddGeneratedMaps())
```

That is the whole setup. Declaring a template **is** the opt-in — `AddGeneratedMaps()` installs the
platform delivery points itself (iOS `OpenUrl` and `ContinueUserActivity`, Android `OnCreate` and
`OnNewIntent`), so your `AppDelegate`, `MainActivity` and `App` classes stay untouched. Windows has
no automatic hook; forward activation to `IAppLinks.Handle(uri)`, which returns an
`AppLinkResult` — `Navigated`, `Blocked` (a [navigation interceptor](#navigation-interceptors)
cancelled it) or `Unhandled` (nothing matched). Treat anything other than `Unhandled` as handled.

Both the `AppDelegate` and `UISceneDelegate` variants are hooked, because `MauiUISceneDelegate`
raises only the Scene-prefixed events and does not forward to the others — so an app that declares
`UIApplicationSceneManifest` would otherwise get dead links. iOS calls one delegate or the other,
never both.

`UseAppLinks(...)` is optional and only changes defaults:

### Push or reset is inferred, not configured

| `registerRoute` | What the route is | An app link |
|---|---|---|
| `false` | `ShellContent` / tab / flyout item in AppShell XAML | resets the stack — `//route` |
| `true` | `Routing.RegisterRoute`'d detail page | pushes onto the current stack |

You already said which it is; the library does not ask twice. On a cold start a pushed route lands
on Shell's default item, or on `AppLinkOptions.DefaultRoot` when you set one.

```csharp
.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseAppLinks(o =>
    {
        o.DefaultRoot = "//main/home";              // back stack for cold-start pushes
        o.ResolveRoute = match => "//somewhere";    // last word on the destination
        o.OnUnhandled = uri => Task.FromResult(false);
    })
)
```

### Manifests

The build validates your manifests and emits a warning containing the exact markup to paste. It
does not edit them — Android's merged manifest names the launcher activity with a CRC64 hash of its
namespace that MSBuild cannot compute, and Apple universal links additionally need an
`apple-app-site-association` file on the domain plus the Associated Domains capability on the App ID.

| Code | Platform | Missing |
|---|---|---|
| **SHINY101** | Android | `[IntentFilter]` for a custom scheme |
| **SHINY102** | Android | Verified (`AutoVerify`) `[IntentFilter]` for a domain |
| **SHINY103** | iOS / MacCatalyst | `CFBundleURLTypes` in `Info.plist` |
| **SHINY104** | iOS / MacCatalyst | `com.apple.developer.associated-domains` in `Entitlements.plist` |
| **SHINY105** | Windows | `windows.protocol` extension in `Package.appxmanifest` |

Set `<ShinyAppLinkValidation>false</ShinyAppLinkValidation>` to silence them. Creating
`Platforms/iOS/Entitlements.plist` is enough on its own — the SDK picks it up without a
`CodesignEntitlements` property.

### Building links to share

When exactly one scheme (or, failing that, one domain) is configured, an outbound builder is
generated per route:

```csharp
var uri = navigator.CreateProductAppLink(id: 42, tab: "reviews");
// myapp://product/42?Tab=reviews
```

### Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| **SHINY005** | Error | Template token has no matching `[ShellProperty]` |
| **SHINY006** | Error | A templated property's type cannot be converted from a URL string |
| **SHINY007** | Error | Two routes declare templates of the same shape |
| **SHINY008** | Warning | `appLinks` declared but no scheme or domain configured |
| **SHINY009** | Warning | A required property is not a token in the template, so links must supply it as a query value |

---

## App Shortcuts

Home screen quick actions (iOS long-press, Android app shortcuts), declared on the route they open:

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

Setting `Shortcut` is what declares the quick action; the other three are optional refinements.
The route becomes the shortcut's id, so there is no magic string to keep in sync and no
hand-written `switch` over activations.

Platform delivery is MAUI's `AppActions` — no `AppDelegate`, `MainActivity` or manifest work.
Whether an activation pushes or resets the stack is inferred from `registerRoute`, exactly as it is
for [App Links](#app-links).

### Routes that need values

An attribute cannot supply a runtime value, so a route with a **required** `[ShellProperty]` cannot
declare a shortcut this way — that is a **SHINY010** error. Register those by hand instead:

```csharp
.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddAppShortcut<ProductViewModel>(
        "Featured",
        icon: "star",
        id: "featured-product",
        configure: vm => vm.Id = 42
    )
)
```

The lambda works even though shortcuts outlive the process: only the **id** is persisted by the
platform, and the registration is rebuilt every launch and looked up by id on activation.

`AddAppShortcut<TViewModel>` is also the way to use shortcuts with source generation turned off —
`AddGeneratedMaps()` simply emits calls to it.

### Localized titles

The declared strings are attribute literals, so they cannot be translated on their own. Register an
`IAppShortcutText` to resolve them at install time, when `CurrentUICulture` is known:

```csharp
public class ResourceShortcutText : IAppShortcutText
{
    public string  GetTitle(string route, string declared)     => AppResources.ResourceManager.GetString(declared) ?? declared;
    public string? GetSubtitle(string route, string? declared) => declared is null ? null : AppResources.ResourceManager.GetString(declared) ?? declared;
}
```

```csharp
.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseShortcutText<ResourceShortcutText>()
)
```

The declared string becomes the resource key, with the literal as its own fallback. It applies to
generated and hand-registered shortcuts alike.

Installed shortcuts keep their text until pushed again, so after a language change call:

```csharp
await appShortcuts.Refresh();   // IAppShortcuts
```

### Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| **SHINY010** | Error | `Shortcut` set on a route with a required `[ShellProperty]` — use `AddAppShortcut(configure:)` |
| **SHINY011** | Warning | More than four shortcuts — iOS silently drops the excess |
| **SHINY012** | Error | A `Shortcut*` property set without `Shortcut` (the title), so nothing is declared |

`AddAppShortcut` logs the SHINY011 equivalent at runtime, since hand-registered shortcuts get no
compile-time check.

---

## AI Integration

Shiny MAUI Shell's source generation produces metadata and navigation methods designed for AI tool calling via [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI). An AI chat client can discover your app's routes, understand their purpose, extract parameters from natural language, and navigate to the correct page — all with just two tools.

### How It Works

1. **Describe your routes** — Add `description` to `[ShellMap]` and `[ShellProperty]` to explain what each page does and what its parameters mean:

```csharp
public enum WorkOrderPriority { Low, Medium, High, Urgent }

[ShellMap<WorkOrderPage>(description: "Use when the user reports something broken, malfunctioning, or needing repair")]
public partial class WorkOrderViewModel : ObservableObject
{
    [ShellProperty("Summarize what is broken based on what the user said", required: true)]
    public string Description { get; set; } = string.Empty;

    [ShellProperty("Infer urgency from tone. Must be: Low, Medium, High, or Urgent", required: true)]
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
}
```

2. **Install `Microsoft.Extensions.AI`** — AI extensions are enabled by default when this package is referenced:

```bash
dotnet add package Microsoft.Extensions.AI
```

3. **Register AI tools in DI** — Use the generated `AddAiTools()` extension:

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddAiTools()          // registers AiMauiShellTools as singleton
);
```

4. **Inject and use `AiMauiShellTools`** — The generated class provides `Prompt` and `Tools` properties:

```csharp
public class ChatViewModel(AiMauiShellTools aiTools)
{
    // Seed the system prompt with route info
    history.Add(new ChatMessage(ChatRole.System, aiTools.Prompt));

    // Use ready-to-use AITool instances
    var options = new ChatOptions { Tools = [.. aiTools.Tools] };
    var response = await chatClient.GetResponseAsync(history, options);
}
```

The AI calls `GetAiToolApplicableGeneratedRoutes` to discover what pages exist and what they do, then calls `NavigateToRoute` with the appropriate route and parameters extracted from the user's message. `NavigateToRoute` dispatches to `NavigateTo<TViewModel>` with direct property setters — no string-based Shell navigation involved. String values from the AI are automatically converted to the target property type (`int`, `bool`, `double`, enums, `DateTime`, etc.).

### GeneratedRouteParameter

Each parameter in the route info includes:

| Field | Description |
|:------|:------------|
| `ParameterName` | The property name (used as key in `NavigateToRoute` args) |
| `Description` | From `[ShellProperty("...")]` — tells the AI what this field means |
| `TypeName` | CLR type (`string`, `int`, `bool`, etc.) — tells the AI what format to use |
| `IsRequired` | Whether the AI must provide this value |

### Sample App — GitHub Copilot Authentication

The sample application includes a working AI chat demo that authenticates via **GitHub Copilot** using the OAuth device flow. This lets anyone with a Copilot subscription test AI-driven navigation using their own account — no API keys to configure.

The flow:
1. User taps **Login with GitHub** — the app requests a device code and opens `github.com/login/device` in the browser
2. User enters the displayed code to authorize
3. The app exchanges the GitHub token for a Copilot API token and creates an `IChatClient` via `Microsoft.Extensions.AI.OpenAI` (the Copilot API is OpenAI-compatible)
4. The chat injects `AiMauiShellTools` which provides `Prompt` and `Tools` for route discovery and navigation

The relevant sample files:
- `Sample/AI/ChatPage.xaml` — Chat UI using `Shiny.Maui.Controls.ChatView`
- `Sample/AI/ChatViewModel.cs` — AI client setup and tool registration
- `Sample/AI/GitHubCopilotAuthService.cs` — Device flow OAuth + token management
- `Sample/AI/TestWorkOrderViewModel.cs` — AI-navigable work order form
- `Sample/AI/ContactFormViewModel.cs` — AI-navigable contact form

---

## Custom Handlers

Optional handlers that are **not registered by default**. Call `Register()` in your `MauiProgram.cs` to opt in.

### Disable Flyout Swipe

Prevents the Shell flyout from opening via swipe gesture while keeping the hamburger button functional:

```csharp
using Shiny.Handlers;

// In MauiProgram.cs, before builder.Build()
DisableShellFlyoutSwipeHandler.Register();
```

| Platform | Behavior |
|:---------|:---------|
| Android | Locks the `DrawerLayout` to `LockModeLockedClosed` |
| iOS / Mac Catalyst | Disables `UIPanGestureRecognizer` on the Shell view hierarchy |
| Windows | No-op (Windows Shell has no swipe flyout) |
