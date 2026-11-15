# Shiny MAUI Shell Code Templates

## Page + ViewModel Template (with Source Generation)

When generating a new page and ViewModel pair, create both files:

### XAML Page
```xml
<!-- Views/{Name}Page.xaml -->
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="{Namespace}.Views.{Name}Page"
             Title="{Binding Title}">
    <VerticalStackLayout Padding="16" Spacing="12">
        <!-- Page content here -->
    </VerticalStackLayout>
</ContentPage>
```

### XAML Code-Behind
```csharp
// Views/{Name}Page.xaml.cs
namespace {Namespace}.Views;

public partial class {Name}Page : ContentPage
{
    public {Name}Page()
    {
        InitializeComponent();
    }
}
```

### ViewModel (Minimal)
```csharp
// ViewModels/{Name}ViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<{Name}Page>("{route}")]
public partial class {Name}ViewModel(INavigator navigator) : ObservableObject
{
    [ObservableProperty]
    string title = "{Page Title}";
}
```

### ViewModel (Full Lifecycle)
```csharp
// ViewModels/{Name}ViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<{Name}Page>("{route}")]
public partial class {Name}ViewModel(INavigator navigator) : ObservableObject,
    IPageLifecycleAware,
    INavigationConfirmation,
    INavigationAware,
    IDisposable
{
    [ObservableProperty]
    string title = "{Page Title}";

    public void OnAppearing()
    {
        // Page is visible - load data, subscribe to events
    }

    public void OnDisappearing()
    {
        // Page hidden - pause operations
    }

    public Task<bool> CanNavigate()
    {
        // Return true to allow navigation, false to block
        return Task.FromResult(true);
    }

    public void OnNavigatingFrom(IDictionary<string, object> parameters)
    {
        // Add/modify parameters before leaving
    }

    public void Dispose()
    {
        // Cleanup subscriptions, timers, etc.
    }
}
```

## Page with Navigation Parameters Template

### ViewModel with ShellProperty
```csharp
// ViewModels/{Name}ViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<{Name}Page>("{route}")]
public partial class {Name}ViewModel(INavigator navigator) : ObservableObject
{
    // Required parameter - source generator will make this a required method parameter
    [ShellProperty]
    [ObservableProperty]
    string {requiredParam};

    // Optional parameter - source generator will give this a default value
    [ShellProperty(required: false)]
    [ObservableProperty]
    int {optionalParam};

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof({RequiredParam}), out var param1))
            {RequiredParam} = param1?.ToString();

        if (query.TryGetValue(nameof({OptionalParam}), out var param2) && param2 is int intVal)
            {OptionalParam} = intVal;
    }
}
```

## Modal Page Template

### XAML
```xml
<!-- Views/{Name}Page.xaml -->
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             Shell.PresentationMode="Modal"
             x:Class="{Namespace}.Views.{Name}Page"
             Title="{Binding Title}">
    <Grid RowDefinitions="Auto,*,Auto" Padding="16">
        <!-- Header -->
        <Label Text="{Binding Title}" FontSize="24" FontAttributes="Bold" />

        <!-- Content -->
        <ScrollView Grid.Row="1">
            <!-- Modal content here -->
        </ScrollView>

        <!-- Actions -->
        <HorizontalStackLayout Grid.Row="2" Spacing="12" HorizontalOptions="End">
            <Button Text="Cancel" Command="{Binding CloseCommand}" />
            <Button Text="Save" Command="{Binding SaveCommand}" />
        </HorizontalStackLayout>
    </Grid>
</ContentPage>
```

### ViewModel
```csharp
// ViewModels/{Name}ViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<{Name}Page>("{route}")]
public partial class {Name}ViewModel(INavigator navigator) : ObservableObject,
    IPageLifecycleAware,
    IDisposable
{
    [ObservableProperty]
    string title = "{Modal Title}";

    public void OnAppearing() { }
    public void OnDisappearing() { }

    [RelayCommand]
    Task Close() => navigator.GoBack();

    [RelayCommand]
    async Task Save()
    {
        // Save logic here
        await navigator.GoBack(("Result", "saved"));
    }

    public void Dispose() { }
}
```

## Dialog Page Template (awaits a typed result)

Use this instead of the modal template when the page exists to collect a value from the user. No
`Shell.PresentationMode` is needed — the registered `IDialogPresenter` decides how it appears
(modally by default; as a popup/overlay card with `UseShinyDialogPresenter()` /
`UseUxDiversDialogPresenter()`). Keep it a `ContentPage` and size the content to what the dialog
actually needs — the overlay presenters show that content in a card, not a full page.

### XAML
```xml
<!-- Views/{Name}Page.xaml -->
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="{Namespace}.Views.{Name}Page"
             Title="{Binding Title}">
    <Grid RowDefinitions="Auto,*,Auto" Padding="16">
        <Label Text="{Binding Title}" FontSize="24" FontAttributes="Bold" />

        <ScrollView Grid.Row="1">
            <!-- selection UI here -->
        </ScrollView>

        <HorizontalStackLayout Grid.Row="2" Spacing="12" HorizontalOptions="End">
            <Button Text="Cancel" Command="{Binding CancelCommand}" />
            <Button Text="OK" Command="{Binding AcceptCommand}" />
        </HorizontalStackLayout>
    </Grid>
</ContentPage>
```

### ViewModel
```csharp
// ViewModels/{Name}ViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<{Name}Page>("{route}")]
public partial class {Name}ViewModel : ObservableObject, IDialogAware<{TResult}>
{
    public event EventHandler<{TResult}>? Completed;
    public event EventHandler? Cancelled;

    [ObservableProperty]
    string title = "{Dialog Title}";

    [ShellProperty("{parameter description}", required: false)]
    public {TResult} Initial { get; set; }

    [ObservableProperty]
    {TResult} selected;

    [RelayCommand]
    void Accept() => this.Completed?.Invoke(this, this.Selected);

    [RelayCommand]
    void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);
}
```

### Call site
```csharp
// generated from [ShellMap] + IDialogAware<T> - no type arguments needed
var result = await navigator.Show{route}Dialog(initial: someValue);

if (result.TryGetValue(out var value))
{
    // user accepted
}
```

## Navigation Builder Usage Template

Pages used in builder chains must be globally registered (`registerRoute: true`, the default). Do NOT use `registerRoute: false` or declare them as `ShellContent` in XAML.

### ViewModel with Builder Navigation
```csharp
[ShellMap<{Name}Page>("{route}")]
public partial class {Name}ViewModel(INavigator navigator) : ObservableObject
{
    [ShellProperty(true)]
    public string Text { get; set; }

    [RelayCommand]
    Task PushChain() => navigator
        .CreateBuilder()
        .Add<FirstViewModel>(x => x.Text = "Page 1")
        .Add<SecondViewModel>(x => x.Arg = "Page 2")
        .Add<ThirdViewModel>(x => x.Text = "Page 3")
        .Navigate();

    [RelayCommand]
    Task PopAndPush() => navigator
        .CreateBuilder()
        .PopBack(2)
        .Add<FirstViewModel>(x => x.Text = "Replaced")
        .Navigate();

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
```

## Tab Badge Template

Use tab badges only for routes that already exist as tabs in the active Shell.

```csharp
// ViewModels/{Name}ViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<{Name}Page>(registerRoute: false)]
public partial class {Name}ViewModel(INavigator navigator) : ObservableObject
{
    [RelayCommand]
    Task ShowBadge() => navigator.SetTabBadge("Inbox", 3);

    [RelayCommand]
    Task ClearBadge() => navigator.ClearTabBadge("Inbox");
}
```

If the tab maps to a ViewModel, prefer the strongly-typed overloads:

```csharp
[RelayCommand]
Task ShowInboxBadge() => navigator.SetTabBadge<InboxViewModel>(7);

[RelayCommand]
Task ClearInboxBadge() => navigator.ClearTabBadge<InboxViewModel>();

// "New", or "" for a dot
[RelayCommand]
Task MarkInboxNew() => navigator.SetTabBadge<InboxViewModel>("New");
```

> Rendered by MAUI 11 on Android, iOS, Mac Catalyst and Windows; a no-op elsewhere. Colors go on the `Tab` in XAML (`BadgeColor`, `BadgeTextColor`).

## Toolbar Badge Template

Toolbar items belong to the page - bind the badge to the ViewModel.

```csharp
[ObservableProperty] string? unreadCount;   // null hides the badge, "" shows a dot
```

```xml
<ContentPage.ToolbarItems>
    <ToolbarItem Text="Inbox"
                 Command="{Binding OpenInboxCommand}"
                 BadgeText="{Binding UnreadCount}"
                 BadgeColor="Red" />
</ContentPage.ToolbarItems>
```

## XAML Navigation Template

Use `Navigate.*` attached properties for simple route-based navigation directly from XAML.

### Single Parameter

```xml
<!-- Views/{Name}Page.xaml -->
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell"
             x:Class="{Namespace}.Views.{Name}Page">
    <ContentPage.ToolbarItems>
        <ToolbarItem Text="Home"
                     shiny:Navigate.Route="MainPage"
                     shiny:Navigate.RelativeNavigation="False" />
    </ContentPage.ToolbarItems>

    <VerticalStackLayout Padding="16" Spacing="12">
        <Button Text="Open Detail"
                shiny:Navigate.Route="Detail"
                shiny:Navigate.ParameterKey="ItemId"
                shiny:Navigate.ParameterValue="{Binding SelectedId}" />
    </VerticalStackLayout>
</ContentPage>
```

### Multiple Parameters

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

`Navigate.Route` currently supports `Button`, `MenuItem`, and `ToolbarItem`.

## Root/Home Page Template (No Route Registration)

For pages declared in AppShell.xaml, use `registerRoute: false`:

```csharp
// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<MainPage>(registerRoute: false)]
public partial class MainViewModel(INavigator navigator) : ObservableObject,
    IPageLifecycleAware
{
    [ObservableProperty]
    string title = "Home";

    public void OnAppearing() { }
    public void OnDisappearing() { }

    [RelayCommand]
    Task NavigateToDetail() => navigator.NavigateTo<DetailViewModel>(vm => vm.ItemId = "123");

    [RelayCommand]
    Task NavigateByRoute() => navigator.NavigateTo("Detail", ("ItemId", "123"));
}
```

## AI-Compatible ViewModel Template

For ViewModels that should be discoverable and navigable by an AI chat agent. The key differences from a standard ViewModel:
- `[ShellMap]` includes a `description` that explains **user intent signals** (not just the page name)
- `[ShellProperty]` descriptions tell the AI how to **infer values** from natural language
- `IQueryAttributable` is **not** needed — `NavigateToRoute` sets `[ShellProperty]` properties directly

### ViewModel
```csharp
// ViewModels/{Name}ViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace {Namespace}.ViewModels;

[ShellMap<{Name}Page>(description: "{Describe when this page should be used based on user intent}")]
public partial class {Name}ViewModel(INavigator navigator) : ObservableObject
{
    [ShellProperty("{Tell AI how to infer this value from what the user said}", required: true)]
    public string {RequiredField} { get; set; } = string.Empty;

    [ShellProperty("{Tell AI how to infer this value, or leave empty}", required: false)]
    public string {OptionalField} { get; set; } = string.Empty;

    [ObservableProperty]
    bool isSubmitted;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof({RequiredField}), out var val1))
            {RequiredField} = val1?.ToString() ?? string.Empty;

        if (query.TryGetValue(nameof({OptionalField}), out var val2))
            {OptionalField} = val2?.ToString() ?? string.Empty;

        OnPropertyChanged(nameof({RequiredField}));
        OnPropertyChanged(nameof({OptionalField}));
    }

    [RelayCommand]
    void Submit() => IsSubmitted = true;

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
```

### Wiring AI Tools (in a chat ViewModel)
```csharp
var options = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(navigator.GetAiToolApplicableGeneratedRoutes),
        AIFunctionFactory.Create(navigator.NavigateToRoute)
    ]
};
```

## List-Detail Navigation Template

### List ViewModel
```csharp
[ShellMap<ItemListPage>(registerRoute: false)]
public partial class ItemListViewModel(INavigator navigator) : ObservableObject,
    IPageLifecycleAware
{
    [ObservableProperty]
    ObservableCollection<ItemModel> items = new();

    [ObservableProperty]
    ItemModel selectedItem;

    public void OnAppearing() => LoadItems();
    public void OnDisappearing() { }

    void LoadItems()
    {
        // Load items from service
    }

    [RelayCommand]
    async Task ItemSelected(ItemModel item)
    {
        if (item == null) return;
        await navigator.NavigateTo<ItemDetailViewModel>(vm => vm.ItemId = item.Id);
    }
}
```

### Detail ViewModel
```csharp
[ShellMap<ItemDetailPage>("ItemDetail")]
public partial class ItemDetailViewModel(INavigator navigator, IDialogs dialogs, IItemService itemService) : ObservableObject,
    IPageLifecycleAware,
    INavigationConfirmation,
    IDisposable
{
    [ShellProperty]
    [ObservableProperty]
    string itemId;

    [ObservableProperty]
    ItemModel item;

    bool isDirty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(ItemId), out var id))
            ItemId = id?.ToString();
    }

    public async void OnAppearing()
    {
        if (!string.IsNullOrEmpty(ItemId))
            Item = await itemService.GetItem(ItemId);
    }

    public void OnDisappearing() { }

    public async Task<bool> CanNavigate()
    {
        if (!isDirty) return true;
        return await dialogs.Confirm("Unsaved Changes", "Discard changes?");
    }

    [RelayCommand]
    async Task Save()
    {
        await itemService.SaveItem(Item);
        isDirty = false;
        await navigator.GoBack(("Refresh", true));
    }

    [RelayCommand]
    Task Cancel() => navigator.GoBack();

    public void Dispose() { }
}
```

## AppShell Template

Your AppShell must inherit from `ShinyShell` (not `Shell`):

### XAML
```xml
<!-- AppShell.xaml -->
<shiny:ShinyShell
    x:Class="{Namespace}.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell"
    xmlns:local="clr-namespace:{Namespace}"
    Title="{App Title}">

    <ShellContent
        Title="Home"
        ContentTemplate="{DataTemplate local:MainPage}"
        Route="MainPage" />

</shiny:ShinyShell>
```

### Code-Behind
```csharp
// AppShell.xaml.cs
using Shiny;

namespace {Namespace};

public partial class AppShell : ShinyShell
{
    public AppShell()
    {
        InitializeComponent();
    }
}
```

## MauiProgram.cs Setup Template

### With Source Generation (Recommended)
```csharp
// MauiProgram.cs
using Shiny;

namespace {Namespace};

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseShinyShell(x => x.AddGeneratedMaps())
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register additional services
        builder.Services.AddSingleton<IItemService, ItemService>();

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

### Manual Registration
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .Add<MainPage, MainViewModel>(registerRoute: false)
        .Add<DetailPage, DetailViewModel>("Detail")
        .Add<SettingsPage, SettingsViewModel>("Settings")
        .Add<ModalPage, ModalViewModel>("Modal")
    )
```
