# API Reference

## Installation

```bash
dotnet add package Shiny.Maui.Shell
```

The NuGet package includes both the runtime library and the source generator. No additional analyzer package is needed.

## Namespace

All public types are in the `Shiny` namespace:
```csharp
using Shiny;
```

## INavigator Interface

The primary navigation service. Injected via DI as a singleton.

```csharp
public interface INavigator
{
    // Fires before navigation occurs - includes the source ViewModel instance
    event EventHandler<NavigationEventArgs>? Navigating;

    // Fires after navigation completes - includes the destination ViewModel instance
    event EventHandler<NavigatedEventArgs>? Navigated;

    // Create a fluent navigation builder for multi-segment navigation
    // fromRoot: if true, builds an absolute URI ("//route"); only works for shell-declared routes
    INavigationBuilder CreateBuilder(bool fromRoot = false);

    // Navigate to a registered route with key-value arguments
    // relativeNavigation: true (default) for relative push, false for absolute "//" navigation
    // bypassInterceptors: skip the registered INavigationInterceptors
    // Returns false when an interceptor cancelled the navigation (a redirect returns true)
    Task<bool> NavigateTo(
        string route,
        bool relativeNavigation = true,
        bool bypassInterceptors = false,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    );

    // Navigate to the page associated with a ViewModel type
    // relativeNavigation: true (default) for relative push, false for absolute "//" navigation
    Task<bool> NavigateTo<TViewModel>(
        Action<TViewModel>? configure = null,
        bool relativeNavigation = true,
        bool bypassInterceptors = false,
        CancellationToken cancellationToken = default,
        params IEnumerable<(string Key, object Value)> args
    );

    // Present the page mapped to TViewModel as a dialog and await its typed result.
    // TViewModel must be mapped to a page AND implement IDialogAware<T>.
    // Prefer the generated Show{Route}Dialog extension - it infers both type arguments.
    Task<DialogResult<T>> ShowDialog<TViewModel, T>(
        Action<TViewModel>? configure = null,
        CancellationToken cancellationToken = default
    ) where TViewModel : class, IDialogAware<T>;

    // Pop to the root page, optionally passing arguments
    Task<bool> PopToRoot(params IEnumerable<(string Key, object Value)> args);
    Task<bool> PopToRoot(bool bypassInterceptors, CancellationToken cancellationToken = default, params IEnumerable<(string Key, object Value)> args);

    // Go back one page, optionally passing arguments
    Task<bool> GoBack(params IEnumerable<(string Key, object Value)> args);

    // Go back multiple pages
    Task<bool> GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args);
    Task<bool> GoBack(int backCount, bool bypassInterceptors, CancellationToken cancellationToken = default, params IEnumerable<(string Key, object Value)> args);

    // Switch to a different Shell instance (replaces Application.MainPage)
    Task SwitchShell(Shell shell);

    // Switch to a Shell resolved from the DI container
    Task SwitchShell<TShell>() where TShell : Shell;

    // Set a numeric badge on a tab route in the active Shell
    Task SetTabBadge(string route, int value);

    // Set a numeric badge using the route mapped to a ViewModel
    Task SetTabBadge<TViewModel>(int value);

    // Set a text badge ("" for a dot) on a tab route in the active Shell
    Task SetTabBadge(string route, string text);

    // Set a text badge ("" for a dot) using the route mapped to a ViewModel
    Task SetTabBadge<TViewModel>(string text);

    // Clear a badge from a tab route in the active Shell
    Task ClearTabBadge(string route);

    // Clear a badge using the route mapped to a ViewModel
    Task ClearTabBadge<TViewModel>();
}
```

## INavigationBuilder Interface

A fluent builder for constructing multi-segment navigation URIs. Created via `INavigator.CreateBuilder()`.

```csharp
public interface INavigationBuilder
{
    // Pop back one or more pages before pushing new segments. Must be called before any Add calls.
    INavigationBuilder PopBack(int count = 1);

    // Add a navigation segment for the specified ViewModel type
    INavigationBuilder Add<TViewModel>() where TViewModel : class;

    // Add a segment with a configure callback invoked on the ViewModel when the page is created
    INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class;

    // Add a segment using a raw route string
    INavigationBuilder Add(string routeName);

    // Skip the registered INavigationInterceptors for this navigation - the fluent form of
    // Navigate(bypassInterceptors: true). Can be called anywhere in the chain.
    INavigationBuilder BypassInterceptors();

    // Execute the navigation. False when an interceptor cancelled it.
    Task<bool> Navigate(bool bypassInterceptors = false, CancellationToken cancellationToken = default);
}
```

### Navigation Builder Constraints

- **All pages in a chain must be globally registered** (`registerRoute: true`, the default). Pages declared as `ShellContent` in XAML cannot be used in multi-segment relative URIs — Shell throws "Relative routing to shell elements is currently not supported".
- **`PopBack()` must be called before `Add()`** — you cannot interleave pops and pushes.
- **`fromRoot: true`** generates a `//` prefix and only works when navigating to shell-declared routes. Global routes cannot be the only page on the stack.

### Usage Examples

```csharp
// Push a chain of pages
await navigator
    .CreateBuilder()
    .Add<OneViewModel>(x => x.Text = "First")
    .Add<AnotherViewModel>(x => x.Arg = "Middle")
    .Add<TwoViewModel>(x => x.Text = "Last")
    .Navigate();

// Pop back 2, then push
await navigator
    .CreateBuilder()
    .PopBack(2)
    .Add<OneViewModel>(x => x.Text = "Replaced")
    .Navigate();
```

## IDialogs Interface

A testable dialog service. Injected via DI as a singleton. Use this instead of `Shell.Current.DisplayAlert`, `Shell.Current.DisplayPromptAsync`, or `Shell.Current.DisplayActionSheet`.

```csharp
public interface IDialogs
{
    // Display an alert dialog
    Task Alert(string? title, string message, string acceptText = "OK");

    // Display a confirmation dialog, returns true if accepted
    Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No");

    // Display a text input prompt, returns entered text or null if cancelled
    Task<string?> Prompt(
        string? title,
        string message,
        string acceptText = "OK",
        string cancelText = "Cancel",
        string? placeholder = null,
        string initialValue = "",
        int maxLength = -1,
        Keyboard? keyboard = null
    );

    // Display an action sheet with multiple options, returns selected button text
    Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons);
}
```

### Usage Examples

```csharp
public class MyViewModel(IDialogs dialogs)
{
    // Alert
    await dialogs.Alert("Error", "Something went wrong");

    // Confirm
    if (await dialogs.Confirm("Delete", "Are you sure?"))
    {
        // delete item
    }

    // Prompt for text input
    var name = await dialogs.Prompt("Name", "Enter your name", placeholder: "John Doe");
    if (name != null)
    {
        // user accepted with a value
    }

    // Prompt with numeric keyboard and max length
    var code = await dialogs.Prompt("Code", "Enter PIN", maxLength: 4, keyboard: Keyboard.Numeric);

    // Action sheet
    var action = await dialogs.ActionSheet("Photo", "Cancel", "Delete", "Take Photo", "Choose from Library");
}
```

## ViewModel Dialogs

### IDialogAware\<T\>

Implemented by a ViewModel that can be presented as a dialog and return a value. `out T` is legal
here because `EventHandler<in TEventArgs>` is contravariant, which puts `T` in a covariant position.

```csharp
public interface IDialogAware<out T>
{
    // Raised by the ViewModel when the user makes a selection
    event EventHandler<T> Completed;

    // Raised by the ViewModel when the user explicitly cancels
    event EventHandler Cancelled;
}
```

Raise exactly one of the two to close the dialog. Raising more than once is harmless — the first
wins. There is no base class: a ViewModel's base slot belongs to `ObservableObject`.

### DialogResult\<T\>

```csharp
public readonly record struct DialogResult<T>(bool IsCancelled, T? Value)
{
    static DialogResult<T> Cancel();
    static DialogResult<T> Complete(T value);

    bool TryGetValue([MaybeNullWhen(false)] out T value);   // false when cancelled
    T ValueOr(T fallback);
}
```

| Outcome | Result |
|---|---|
| ViewModel raised `Completed` | `IsCancelled == false`, `Value` set |
| ViewModel raised `Cancelled` | `IsCancelled == true` |
| User dismissed (hardware back, iOS swipe-down, tap-outside) | `IsCancelled == true` |
| The passed `CancellationToken` fired | throws `OperationCanceledException` |

Every dismissal path completes the await — a dialog closed without either event being raised reports
cancellation rather than hanging.

### Usage Example

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

// call site - generated extension, no type arguments
var result = await navigator.ShowPickColorDialog(preset: "Violet");
if (result.TryGetValue(out var colour))
    this.Selected = colour;
```

### IDialogPresenter

Controls how the dialog page appears. The default `ShellModalDialogPresenter` pushes it onto Shell's
modal stack, so the page does **not** need `Shell.PresentationMode="Modal"` in XAML.

```csharp
public interface IDialogPresenter
{
    // Present the page. The returned Task must complete once the page is gone -
    // whether the user dismissed it, or `dismiss` asked for teardown.
    // Must NOT throw OperationCanceledException when `dismiss` fires.
    // Responsible for dispatching to the main thread.
    Task Present(Page page, object viewModel, CancellationToken dismiss);
}
```

Register with `ShinyAppBuilder.UseDialogPresenter<TPresenter>()`.

#### Built-in presenters

| Presenter | Package | Registration | Presentation |
|:----------|:--------|:-------------|:-------------|
| `ShellModalDialogPresenter` (default) | `Shiny.Maui.Shell` | — | Page on Shell's modal stack |
| `ShinyOverlayDialogPresenter` | `Shiny.Maui.Shell.ShinyDialogs` | `UseShinyDialogPresenter(Action<ShinyDialogPresenterOptions>?)` | Themed card over a dimmed backdrop, inside the current page |
| `UxDiversDialogPresenter` | `Shiny.Maui.Shell.UxDiversDialogs` | `UseUxDiversDialogPresenter(Action<UxDiversDialogPresenterOptions>?)` | UXDivers `PopupPage` over a dimmed backdrop |

Both overlay presenters share these options:

```csharp
double     BackdropOpacity      // 0.5
Color?     BackdropColor        // null = theme scrim (Shiny) / PopupBackdropColor (UXDivers)
bool       DismissOnBackdropTap // true - a backdrop tap reports IsCancelled
double     CornerRadius         // 16
Color?     CardBackgroundColor  // null = theme surface (Shiny) / PopupBorderColor (UXDivers)
double     MaxWidth             // 420
Thickness  Margin               // 24
```

Plus, per presenter: `uint AnimationDuration` + `Action<Border>? ConfigureCard` (Shiny), and
`int AnimationDuration`, `bool AvoidKeyboard`, `Action<PopupPage>? ConfigurePopup` (UXDivers).

They present the dialog page's `Content`, so the page must be a `ContentPage` with content set - both
throw `InvalidOperationException` otherwise. The page underneath stays on screen, so it does **not**
get `OnDisappearing`/`OnAppearing` around the dialog.

#### ViewDialogPresenter

Base class for presenting into a host that takes a `View` rather than a `Page` - a popup, a bottom
sheet, an overlay. It handles what the page would otherwise do for you: setting the binding context on
the detached content, raising `IPageLifecycleAware`, disposing an `IDisposable` ViewModel, and giving
the content back to its page.

```csharp
public abstract class ViewDialogPresenter : IDialogPresenter
{
    // Called on the main thread, binding context already set. Complete once the content
    // is gone; detach it from your host before returning. Never throw on `dismiss`.
    protected abstract Task PresentView(View content, object viewModel, CancellationToken dismiss);
}
```

### Constraints
- The dialog ViewModel must be mapped to a page (`[ShellMap<TPage>]` + `AddGeneratedMaps()`, or
  `Add<TPage, TViewModel>()`). `ShowDialog` throws `InvalidOperationException` otherwise.
- `IPageLifecycleAware.OnAppearing`/`OnDisappearing` and `IDisposable.Dispose` fire on the dialog
  ViewModel; with the default modal presenter the page underneath gets `OnDisappearing` on open and
  `OnAppearing` on close (the overlay presenters leave it on screen, so it gets neither).
- `INavigationAware`, `INavigationConfirmation`, and `INavigator.Navigating`/`Navigated` are
  deliberately not involved — showing a dialog is not a navigation stack mutation.
- The dialog ViewModel is disposed as the page detaches, marginally before `ShowDialog` returns. The
  returned `DialogResult<T>` is unaffected; do not use the ViewModel instance after the await.

## Navigation Events

### NavigationEventArgs (pre-navigation)

Fired via `INavigator.Navigating` before navigation occurs. Provides the source ViewModel instance.

```csharp
public record NavigationEventArgs(
    string? FromUri,                                  // Current location URI
    object? FromViewModel,                            // Source ViewModel instance (cast as needed)
    string ToUri,                                     // Destination route URI
    NavigationType NavigationType,                    // Push, SetRoot, GoBack, or PopToRoot
    IReadOnlyDictionary<string, object> Parameters    // Navigation parameters
);
```

### NavigatedEventArgs (post-navigation)

Fired via `INavigator.Navigated` after navigation completes and the destination page's ViewModel is resolved. Provides the destination ViewModel instance.

```csharp
public record NavigatedEventArgs(
    string ToUri,                                     // Destination route URI
    object? ToViewModel,                              // Destination ViewModel instance (cast as needed)
    NavigationType NavigationType,                    // Push, SetRoot, GoBack, or PopToRoot
    IReadOnlyDictionary<string, object> Parameters    // Navigation parameters
);
```

### NavigationType Enum

```csharp
public enum NavigationType
{
    Push,
    SetRoot,
    GoBack,
    PopToRoot,
    SwitchShell
}
```

### Usage

```csharp
navigator.Navigating += (sender, args) =>
{
    // Access the source ViewModel
    if (args.FromViewModel is MyViewModel vm)
        Console.WriteLine($"Leaving {vm.Title}");
};

navigator.Navigated += (sender, args) =>
{
    // Access the destination ViewModel
    if (args.ToViewModel is DetailViewModel detail)
        Console.WriteLine($"Arrived at {detail.ItemId}");
};
```

### Usage Examples

```csharp
public class MyViewModel(INavigator navigator)
{
    // Route-based navigation
    await navigator.NavigateTo("Detail", args: [("ItemId", "abc"), ("Mode", "edit")]);

    // ViewModel-based navigation
    await navigator.NavigateTo<DetailViewModel>(vm => vm.ItemId = "abc");

    // Absolute navigation (navigates to root route "//Detail")
    await navigator.NavigateTo("Detail", relativeNavigation: false);
    await navigator.NavigateTo<DetailViewModel>(relativeNavigation: false);

    // Go back with result
    await navigator.GoBack(("Result", selectedValue));

    // Go back 2 pages
    await navigator.GoBack(2);

    // Pop entire stack to root
    await navigator.PopToRoot();

    // Switch to a different Shell instance
    await navigator.SwitchShell(new MainAppShell());

// Switch to a Shell resolved from DI
await navigator.SwitchShell<MainAppShell>();

// Set or clear a badge on an existing tab - a count, text, or "" for a dot
await navigator.SetTabBadge("Inbox", "New");
await navigator.SetTabBadge("Inbox", 3);
await navigator.SetTabBadge<InboxViewModel>(7);
await navigator.ClearTabBadge("Inbox");
await navigator.ClearTabBadge<InboxViewModel>();

// Multi-segment navigation via builder
await navigator
    .CreateBuilder()
        .Add<OneViewModel>(x => x.Text = "First")
        .Add<TwoViewModel>(x => x.Text = "Last")
        .Navigate();
}
```

### Tab Badge Constraints

- Badge APIs only work for routes already present as tabs in the active Shell
- They set MAUI 11 `BaseShellItem.BadgeText` on the tab's `ShellSection`; rendered on Android, iOS, Mac Catalyst and Windows (Windows draws non-numeric text as a dot), a no-op elsewhere
- Style with `BadgeColor` / `BadgeTextColor` on the `Tab` in XAML; don't also bind `BadgeText` on that tab
- Toolbar badges: bind `ToolbarItem.BadgeText` (`null` hides, `""` is a dot) - there is no `INavigator` API for them

## IPageLifecycleAware Interface

Provides page appearing/disappearing lifecycle hooks on ViewModels.

```csharp
public interface IPageLifecycleAware
{
    // Called when the page becomes visible (or re-appears after navigation back)
    void OnAppearing();

    // Called when the page is hidden or removed from the navigation stack
    void OnDisappearing();
}
```

## INavigationInterceptor Interface

App-wide navigation guard. Registered with `AddNavigationInterceptor<T>()`; multiple interceptors
run in registration order and the first to cancel or redirect wins.

```csharp
public interface INavigationInterceptor
{
    // uri:       the destination Shell URI, exactly as Shell will receive it
    // viewModel: the DESTINATION ViewModel, already resolved and populated. null for unmapped
    //            routes and for Shell-driven navigation (tab taps, hardware back).
    // cancellationToken: the token passed to the navigation call
    Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    );

    // Lowest runs first; ties keep registration order. Defaults to 0 - override to force a guard
    // ahead of (or behind) another.
    int Order => 0;
}

public class NavigationInterceptorResult
{
    bool CancelNavigation { get; set; }     // stop the navigation; wins over RedirectUri
    string? RedirectUri { get; set; }       // go somewhere else instead
    Type? RedirectViewModelType { get; set; } // refactor-safe alternative to RedirectUri
    bool RedirectRelative { get; set; }     // typed redirect: push (true) or reset stack (false)

    static NavigationInterceptorResult Continue { get; }
    static NavigationInterceptorResult Cancel();
    static NavigationInterceptorResult Redirect(string uri);
    static NavigationInterceptorResult Redirect<TViewModel>(bool relativeNavigation = false);
}
```

### Usage
```csharp
public class AuthNavigationInterceptor(IAuthService auth) : INavigationInterceptor
{
    public Task<NavigationInterceptorResult> InterceptNavigationAsync(string uri, object? viewModel)
        => Task.FromResult(auth.IsAuthorized || uri.Contains("Login")
            ? NavigationInterceptorResult.Continue
            : NavigationInterceptorResult.Redirect<LoginViewModel>()
        );
}

// MauiProgram.cs
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddNavigationInterceptor<AuthNavigationInterceptor>()
);
```

### Coverage

| Path | Intercepted | `viewModel` |
|:-----|:------------|:------------|
| `NavigateTo(route)` | yes | resolved from the route mapping |
| `NavigateTo<TViewModel>(configure)` | yes | your instance, after `configure` |
| `CreateBuilder()...Navigate()` | yes | the last segment's ViewModel |
| `GoBack` / `PopToRoot` | yes | the existing ViewModel from the stack |
| App links / app shortcuts | yes | the ViewModel with link values applied |
| Tab taps, flyout, hardware back | yes | `null` |
| `ShowDialog`, `SwitchShell` | no | — |

### Constraints
- Registered as singletons — hold no per-navigation state in fields.
- `bypassInterceptors: true` on any `INavigator` method (or `INavigationBuilder.Navigate`, or the
  fluent `CreateBuilder().BypassInterceptors()`) skips the chain — use it for the navigation a guard
  itself performs.
- Navigation methods return `Task<bool>`: false means an interceptor cancelled. A redirect returns
  true — the navigation happened, somewhere else.
- A redirect re-runs the entire chain against the new URI; redirecting to the URI already being
  navigated to is ignored; a genuine loop throws after 10 redirects.
- A single leading `/` in `RedirectUri` is promoted to `//` (stack reset). `..` prefixes go back.
- Exceptions propagate to the caller and cancel the navigation.
- Runs on the main thread — dialogs can be awaited directly.

## INavigationContextAccessor Interface

The rest of the navigation being intercepted, most usefully the page being left.

```csharp
public interface INavigationContextAccessor
{
    NavigationContext? Current { get; }  // null outside an interceptor call
}

public record NavigationContext(
    string? FromUri,
    object? FromViewModel,
    string ToUri,
    NavigationType NavigationType,
    IReadOnlyDictionary<string, object> Parameters,
    int RedirectCount
)
{
    // Forward (push) / Back (GoBack, PopToRoot) / Root (SetRoot, SwitchShell)
    NavigationDirection Direction { get; }
}
```

`NavigationDirection` is also on `NavigationEventArgs` and `NavigatedEventArgs`, and any
`NavigationType` converts with `navigationType.GetDirection()`.

```csharp
public enum NavigationDirection { Forward, Back, Root }
```

## INavigationConfirmation Interface

Allows a ViewModel to block navigation away from its page. Asked **only for user-driven Shell
navigation** — a tab tap, a flyout item, the hardware back button — and asked before the
`INavigationInterceptor` chain. Programmatic navigation (`INavigator` calls, app links, shortcuts)
does not consult it; guard those with an interceptor.

```csharp
public interface INavigationConfirmation
{
    // Return true to allow navigation, false to block it
    Task<bool> CanNavigate();
}
```

### Usage
```csharp
public async Task<bool> CanNavigate()
{
    if (!hasUnsavedChanges)
        return true;

    return await dialogs.Confirm("Unsaved Changes", "Discard changes?");
}
```

## INavigationAware Interface

Allows a ViewModel to add or modify navigation parameters before the page navigates away.

```csharp
public interface INavigationAware
{
    // Called before navigation. Mutate the parameters dictionary to pass data back.
    void OnNavigatingFrom(IDictionary<string, object> parameters);
}
```

### Usage
```csharp
public void OnNavigatingFrom(IDictionary<string, object> parameters)
{
    parameters["LastViewed"] = CurrentItemId;
    parameters["Timestamp"] = DateTime.UtcNow;
}
```

## IAppLinks Interface

Handles inbound deep links. Registered by `UseAppLinks()`; most apps never call it directly.

```csharp
public interface IAppLinks
{
    // Resolves the URI to a route and navigates. True when matched (or queued for a
    // Shell that has not started yet).
    // Navigated / Blocked (an interceptor cancelled it) / Unhandled (nothing matched)
    Task<AppLinkResult> Handle(Uri uri);

    // Resolves without navigating - for testing, or for inspecting a link before acting.
    bool TryResolve(Uri uri, out AppLinkMatch match);
}

public record AppLinkMatch(
    string Route,
    string Template,
    Type ViewModelType,
    IReadOnlyDictionary<string, string> Values,   // case-insensitive
    Uri Uri
);
```

Call `Handle` yourself only where a platform hook cannot be reached automatically - currently
Windows protocol activation.

## AppLinkOptions Class

```csharp
public class AppLinkOptions
{
    // Absolute route a relative link is pushed onto when the app is cold-started by it.
    // Null means the link lands on Shell's own first item.
    public string? DefaultRoot { get; set; }

    // Last word on the destination, overriding the registerRoute inference and DefaultRoot.
    public Func<AppLinkMatch, string>? ResolveRoute { get; set; }

    // Called when nothing matched. Return true to report the link as handled.
    // Default: logs a warning and leaves the user where they are.
    public Func<Uri, Task<bool>>? OnUnhandled { get; set; }
}
```

### Constraints

- Templates are declared on `[ShellMap(appLinks: [...])]` — there is no separate attribute and no
  runtime registration API you should call.
- Push vs. reset is **inferred** from `registerRoute` and is not configurable per template; use
  `ResolveRoute` for the rare Shell whose structure breaks that convention.
- A required value that is missing or unparseable makes the template fail to bind. The router tries
  the next matching template, then `OnUnhandled`. It never throws at the caller.
- Matching is ordered by specificity: literal segments beat tokens, so `product/featured` is tried
  before `product/{id}`. Two templates of the same shape are a **SHINY007** build error.

## AppLinkRoutes Class

Pure helper exposing the push-vs-reset rule so it can be reasoned about and tested directly.

```csharp
public static class AppLinkRoutes
{
    public static string Build(
        AppLinkMatch match,
        RegisteredAppLink link,
        bool coldStart,
        AppLinkOptions options
    );
}
```

## App Shortcuts

Home screen quick actions. Declared with named properties on `[ShellMap]`; platform delivery is
MAUI's `AppActions`.

```csharp
// on ShellMapAttribute<TPage>
public string? Shortcut { get; set; }          // title - setting this declares the shortcut
public string? ShortcutSubtitle { get; set; }  // iOS only in practice
public string? ShortcutIcon { get; set; }      // system icon (iOS) / drawable (Android)
public int ShortcutOrder { get; set; }
```

```csharp
public record RegisteredAppShortcut(
    string Id,                  // the route unless overridden
    string Route,
    Type ViewModelType,
    bool RegisterRoute,         // drives push vs. reset, as for app links
    string Title,
    string? Subtitle,
    string? Icon,
    int Order,
    Action<object>? Configure
);

public class AppShortcutRegistry
{
    public const int PlatformMaximum = 4;
    public IReadOnlyList<RegisteredAppShortcut> Shortcuts { get; }   // in display order
    public void Add(RegisteredAppShortcut shortcut);
    public RegisteredAppShortcut? Find(string id);                    // case-sensitive
}
```

```csharp
// Localization - the declared strings are attribute literals and cannot be translated alone
public interface IAppShortcutText
{
    string  GetTitle(string route, string declared);
    string? GetSubtitle(string route, string? declared);
}

public interface IAppShortcuts
{
    Task Refresh();   // re-resolve text and re-push; call after a language change
}
```

Register with `UseShortcutText<T>()`. Resolution runs at install time (so `CurrentUICulture` is
known) and again on `Refresh()`. The default returns the declared strings verbatim.

### Constraints

- A route with a **required** `[ShellProperty]` cannot declare a shortcut via the attribute
  (**SHINY010**) — an attribute cannot supply a runtime value. Use
  `AddAppShortcut<TViewModel>(configure: ...)`.
- The `configure` lambda survives restarts because only the **id** is persisted by the platform;
  the registration is rebuilt each launch and resolved by id.
- `Find` is case-sensitive — the id round-trips through the platform verbatim.
- iOS shows at most four and drops the rest silently.
- Shortcut titles come from attribute literals, so localization goes through `IAppShortcutText`
  rather than the attribute. Installed shortcuts keep their text until `Refresh()` is called.

## ShinyAppBuilder Class

Fluent builder for registering Page-to-ViewModel mappings and configuring shell services. Used inside `UseShinyShell()`.

```csharp
public sealed class ShinyAppBuilder(MauiAppBuilder builder)
{
    // Register a Page-ViewModel pair
    // route: optional route name (defaults to page class name)
    // registerRoute: set false for pages already in AppShell.xaml
    ShinyAppBuilder Add<TPage, TViewModel>(string? route = null, bool registerRoute = true)
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged;

    // Replace the default IDialogs provider with a custom implementation.
    // Registered as a singleton. Call order does not matter — UseDialogs<>
    // always wins over the default ShellDialogs (registered via TryAddSingleton).
    ShinyAppBuilder UseDialogs<TDialog>() where TDialog : class, IDialogs;

    // Replace the default dialog presenter (ShellModalDialogPresenter) used by ShowDialog.
    // Same registration semantics as UseDialogs.
    ShinyAppBuilder UseDialogPresenter<TPresenter>() where TPresenter : class, IDialogPresenter;

    // Enable inbound app links and install the platform delivery points (iOS OpenUrl and
    // ContinueUserActivity, Android OnCreate and OnNewIntent). Templates themselves come from
    // [ShellMap(appLinks: [...])] via AddGeneratedMaps(). Windows has no automatic hook.
    ShinyAppBuilder UseAppLinks(Action<AppLinkOptions>? configure = null);

    // Called by the generated AddGeneratedMaps() - do not call by hand.
    ShinyAppBuilder AddAppLink<TViewModel>(
        string template,
        Func<TViewModel, IReadOnlyDictionary<string, string>, bool> apply
    ) where TViewModel : class;

    // Register a navigation guard (singleton). Runs on every navigation - see
    // INavigationInterceptor. Overloads take an instance or an inline delegate.
    ShinyAppBuilder AddNavigationInterceptor<TInterceptor>() where TInterceptor : class, INavigationInterceptor;
    ShinyAppBuilder AddNavigationInterceptor(INavigationInterceptor interceptor);
    ShinyAppBuilder AddNavigationInterceptor(Func<string, object?, Task<NavigationInterceptorResult>> interceptor);

    // Route registration lookup - page/ViewModel types plus whether the route was registered
    // with Shell (false means it is declared in AppShell XAML).
    (bool RegisterRoute, Type PageType, Type ViewModelType)? GetRouteInfo(string route);

    // Register a quick action by hand. The generated AddGeneratedMaps() emits calls to this, so
    // shortcuts still work with source generation disabled. configure handles routes whose
    // ViewModel needs values the attribute cannot supply.
    ShinyAppBuilder AddAppShortcut<TViewModel>(
        string title,
        string? subtitle = null,
        string? icon = null,
        int order = 0,
        string? id = null,
        Action<TViewModel>? configure = null
    ) where TViewModel : class;
}
```

### UseDialogs Example

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseDialogs<MyCustomDialogs>()
);
```

### Constraints
- `TPage` must inherit from `Microsoft.Maui.Controls.Page`
- `TViewModel` must implement `INotifyPropertyChanged`
- Both are registered as Transient in DI automatically

## Attributes

### ShellMapAttribute\<TPage\>

Marks a ViewModel class for source generation. Applied to the ViewModel class.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ShellMapAttribute<TPage>(
    string? route = null,         // Route name — must be a valid C# identifier; used as generated constant and method name
    bool registerRoute = true,    // Set false for AppShell.xaml pages
    string? description = null    // Description for AI tool metadata — used in GetGeneratedRouteInfo and [Description] attributes
) : Attribute;
```

The `route` parameter drives naming:
- `[ShellMap<DetailPage>("Detail")]` → `Routes.Detail`, `NavigateToDetail(...)`
- `[ShellMap<HomePage>]` (no route) → `Routes.Home`, `NavigateToHome(...)`

Invalid route names (hyphens, spaces, leading digits) produce a **SHINY001** compiler error.

### ShellPropertyAttribute

Marks a ViewModel property as a navigation parameter for source generation.

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ShellPropertyAttribute(
    string? description = null,   // Description for AI tool metadata and [Description] attributes
    bool required = true          // Whether this parameter is required in generated methods
) : Attribute;
```

### Source Generation Output

Given this input:
```csharp
[ShellMap<DetailPage>("Detail")]
public partial class DetailViewModel : ObservableObject
{
    [ShellProperty] public string ItemId { get; set; }
    [ShellProperty(required: false)] public int Page { get; set; }
}
```

The source generator produces:

**Routes.g.cs:**
```csharp
public static class Routes
{
    public const string Detail = "Detail";
}
```

**NavigationExtensions.g.cs:**
```csharp
public static class NavigationExtensions
{
    public static Task<bool> NavigateToDetail(this INavigator navigator, string itemId, int page = default, bool relativeNavigation = true, bool bypassInterceptors = false, CancellationToken cancellationToken = default)
    {
        return navigator.NavigateTo<DetailViewModel>(x =>
        {
            x.ItemId = itemId;
            x.Page = page;
        });
    }
}
```

**NavigationBuilderExtensions.g.cs** (uses string literals, not `Routes.*`):
```csharp
public static class NavigationBuilderExtensions
{
    public static ShinyAppBuilder AddGeneratedMaps(this ShinyAppBuilder builder)
    {
        builder.Add<DetailPage, DetailViewModel>("Detail");
        return builder;
    }
}
```

### AiExtensions.g.cs (AI Integration)

When AI extensions are enabled (`ShinyMauiShell_GenerateAiExtensions=true`, requires `Microsoft.Extensions.AI`), the source generator also produces:

```csharp
public static class AiExtensions
{
    // Always generated (not AI-specific)
    [Description("This provides a list of routes throughout the application")]
    public static GeneratedRouteInfo[] GetGeneratedRouteInfo(this INavigator navigator);

    // AI extensions below only generated when opted in:

    // Returns only routes with a description AND at least one parameter
    [Description("This provides a list of AI tool applicable routes")]
    public static GeneratedRouteInfo[] GetAiToolApplicableGeneratedRoutes(this INavigator navigator);

    // Pre-formatted prompt string for seeding AI system messages
    public static string AiRoutePrompt(this INavigator navigator);

    // AI-friendly navigation using switch dispatch to NavigateTo<TViewModel>
    // String values are automatically converted to the target property type:
    // string (direct), int/long/short/byte/float/double/decimal (T.Parse),
    // bool (bool.Parse), enums (Enum.Parse case-insensitive),
    // Guid/DateTime/DateTimeOffset/TimeSpan (T.Parse), Uri (new Uri),
    // other types (Convert.ChangeType fallback)
    [Description("Navigate to a route in the application, passing parameters as key-value pairs. Returns a confirmation message.")]
    public static Task<string> NavigateToRoute(
        this INavigator navigator,
        [Description("The route name to navigate to")] string route,
        [Description("Route parameters as key-value pairs")] Dictionary<string, string>? args = null);

    // Ready-to-use AITool instances for ChatOptions.Tools
    public static IList<AITool> GetAiTools(this INavigator navigator);
}
```

### GeneratedRouteInfo / GeneratedRouteParameter

Route metadata records used by the generated AI extensions:

```csharp
namespace Shiny.Infrastructure;

public record GeneratedRouteInfo(
    string Route,                        // Route name from [ShellMap]
    string Description,                  // Description from [ShellMap] (empty if not provided)
    GeneratedRouteParameter[] Parameters // All [ShellProperty] properties
);

public record GeneratedRouteParameter(
    string ParameterName,                // Property name (used as key in NavigateToRoute args)
    string Description,                  // From [ShellProperty("...")] (empty if not provided)
    string TypeName,                     // CLR type name: "string", "int", "bool", etc.
    bool IsRequired                      // From [ShellProperty(required: ...)]
);
```

### Configuring Source Generation

Disable individual generated files via MSBuild properties:

| Property | Default | Controls |
|---|---|---|
| `ShinyMauiShell_GenerateRouteConstants` | `true` | `Routes.g.cs` |
| `ShinyMauiShell_GenerateNavExtensions` | `true` | `NavigationExtensions.g.cs`, `AiExtensions.g.cs`, and all builder extensions |
| `ShinyMauiShell_GenerateAiExtensions` | `false` | `GetAiToolApplicableGeneratedRoutes`, `NavigateToRoute`, `GetAiTools()`, and `AiRoutePrompt`. Requires `Microsoft.Extensions.AI` (**SHINY003** error if missing) |
| `ShinyMauiShell_AiExtensionsClassName` | `AiExtensions` | Class name for the route info/AI extensions class |
| `ShinyMauiShell_AiNavigateMethodName` | `NavigateToRoute` | Method name for the AI-friendly navigate method |

`NavigationBuilderExtensions.g.cs` is always generated.

## ShinyShell Base Class

Your `AppShell` must inherit from `ShinyShell` instead of `Shell`. This ensures the initial page's BindingContext is set deterministically via Shell's own `OnNavigated` lifecycle — avoiding a race condition where the `Application.PageAppearing` event can fire before the handler is registered.

```csharp
// AppShell.xaml.cs
using Shiny;

public partial class AppShell : ShinyShell
{
    public AppShell()
    {
        InitializeComponent();
    }
}
```

```xml
<!-- AppShell.xaml -->
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

## XAML Navigation Types

Use `Navigate` attached properties for route-based navigation directly from XAML.

```csharp
public static class Navigate
{
    public static readonly BindableProperty RouteProperty;
    public static readonly BindableProperty RelativeNavigationProperty;
    public static readonly BindableProperty ParameterKeyProperty;
    public static readonly BindableProperty ParameterValueProperty;
    public static readonly BindableProperty ParametersProperty;

    public static string? GetRoute(BindableObject bindable);
    public static void SetRoute(BindableObject bindable, string? value);
    public static bool GetRelativeNavigation(BindableObject bindable);
    public static void SetRelativeNavigation(BindableObject bindable, bool value);
    public static string? GetParameterKey(BindableObject bindable);
    public static void SetParameterKey(BindableObject bindable, string? value);
    public static object? GetParameterValue(BindableObject bindable);
    public static void SetParameterValue(BindableObject bindable, object? value);
    public static NavigationParameters? GetParameters(BindableObject bindable);
    public static void SetParameters(BindableObject bindable, NavigationParameters? value);
}

public sealed class NavigationParameters : List<NavigationParameter>
{
}

public sealed class NavigationParameter : BindableObject
{
    public string? Key { get; set; }
    public object? Value { get; set; }
}
```

### XAML Navigation Usage

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

### XAML Navigation Constraints

- Supported controls: `Button`, `MenuItem`, `ToolbarItem`
- `Navigate.Route` must be set before click execution
- Parameter items require a non-empty `Key`
- The implementation resolves `INavigator` from MAUI services and delegates to `NavigateTo(route, relativeNavigation, args...)`
- Keep XAML navigation generic; strongly-typed generated navigation remains a C# feature

## ShellServices Record

A convenience aggregate that bundles the shell services together, so a single constructor parameter is enough when a class needs both.

```csharp
public record ShellServices(
    INavigator Navigator,
    IDialogs Dialogs
);
```

Registered as a singleton by `UseShinyShell()`. Inject directly:

```csharp
public class MyViewModel(ShellServices shell)
{
    async Task DoWork()
    {
        await shell.Dialogs.Alert("Done", "Work complete");
        await shell.Navigator.GoBack();
    }
}
```

Shiny Shell marshals to the UI thread with MAUI's `MainThread`; there is no `IMainThread` (removed in v8).

## Extension Method

### UseShinyShell

Configures Shiny MAUI Shell on the `MauiAppBuilder`.

```csharp
public static MauiAppBuilder UseShinyShell(
    this MauiAppBuilder builder,
    Action<ShinyAppBuilder> navBuilderAction
);
```

Registers:
- `INavigator` as singleton
- `IDialogs` as singleton (default `ShellDialogs`, replaceable via `UseDialogs<>()`)
- `ShellServices` as singleton (aggregate of `INavigator`, `IDialogs`)
- `IMauiInitializeService` for lifecycle hooks
- `ShinyAppBuilder` as singleton
- All mapped Pages and ViewModels as transient

## IQueryAttributable (MAUI Built-in)

Standard MAUI interface for receiving navigation parameters. Only needed when using string-based `NavigateTo(route, args)` with tuple parameters. **Not required** when using `[ShellProperty]` — the source-generated navigation methods set properties directly.

```csharp
// From Microsoft.Maui.Controls
public interface IQueryAttributable
{
    void ApplyQueryAttributes(IDictionary<string, object> query);
}
```

## IDisposable (System)

When implemented on a ViewModel, `Dispose()` is called when the page is permanently removed from the navigation stack.

## Troubleshooting

### ViewModel not bound to Page
- Ensure your AppShell inherits from `ShinyShell`, not `Shell`
- Ensure the Page-ViewModel pair is registered via `Add<TPage, TViewModel>()` or `[ShellMap]` + `AddGeneratedMaps()`
- Check that `UseShinyShell()` is called in MauiProgram.cs

### Navigation parameters not received
- When using `[ShellProperty]`, properties are set directly by generated methods — no `IQueryAttributable` needed
- When using string-based `NavigateTo(route, args)` with tuples, the ViewModel must implement `IQueryAttributable`
- Parameter keys are case-sensitive and must match property names

### Page not found during navigation
- Pages in AppShell.xaml should use `registerRoute: false`
- Pages not in AppShell.xaml need route registration (default behavior)
- Verify the route string matches exactly

### Tab badge not showing
- Ensure the route already exists as a tab in the active Shell
- Badge APIs do not create tabs or attach to non-tab pages
- Verify the current platform is Android, iOS, Mac Catalyst, or Windows

### XAML navigation not firing
- `Navigate.Route` only works on `Button`, `MenuItem`, and `ToolbarItem`
- Ensure the page has MAUI services available through the handler/app service provider
- If using `Navigate.Parameters`, every `NavigationParameter` needs a non-empty `Key`

### Source generator not producing output
- ViewModel class must be `partial`
- Ensure `Shiny.Maui.Shell` NuGet is installed (includes the generator)
- Check that `[ShellMap<TPage>]` attribute is applied to the class
- Route names must be valid C# identifiers — check for **SHINY001** errors
- Route constants and nav extensions can be disabled via `ShinyMauiShell_GenerateRouteConstants` and `ShinyMauiShell_GenerateNavExtensions` MSBuild properties
- AI extensions are disabled by default — set `ShinyMauiShell_GenerateAiExtensions` to `true` and install `Microsoft.Extensions.AI`. Missing the package produces **SHINY003**
- Customize AI class/method names via `ShinyMauiShell_AiExtensionsClassName` and `ShinyMauiShell_AiNavigateMethodName`
- Clean and rebuild the project

### OnAppearing/OnDisappearing not firing
- ViewModel must implement `IPageLifecycleAware`
- Verify the ViewModel is bound to the Page (check BindingContext)

### CanNavigate not called
- ViewModel must implement `INavigationConfirmation`
- Only fires when navigating away from the page (not when navigating to it)
