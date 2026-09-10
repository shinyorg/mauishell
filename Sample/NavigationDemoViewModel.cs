using Shiny;

namespace Sample;

[ShellMap<NavigationDemoPage>]
public partial class NavigationDemoViewModel(
    INavigator navigator
) : ObservableObject, IQueryAttributable, INavigationAware
{
    [ObservableProperty] string arg = "Hello";

    [NotifyPropertyChangedFor(nameof(HasBackResult))]
    [ObservableProperty] string? backResult;
    public bool HasBackResult => !string.IsNullOrWhiteSpace(BackResult);

    [RelayCommand]
    Task PushByRoute() => navigator.NavigateTo(
        nameof(DetailPage),
        args: [("Text", this.Arg)]
    );

    [RelayCommand]
    Task PushByViewModel() => navigator.NavigateTo<DetailViewModel>(
        args: [("Text", this.Arg)]
    );

    [RelayCommand]
    Task PushByViewModelConfigure() => navigator.NavigateTo<DetailViewModel>(
        x => x.Text = $"{this.Arg} (configured)"
    );

    [RelayCommand]
    Task GoBack() => navigator.GoBack();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("BackArg", out var value))
            this.BackResult = value?.ToString();
    }

    public void OnNavigatingFrom(IDictionary<string, object> parameters) { }
}
