using Shiny;

namespace Sample;

/// <summary>
/// The place to watch <see cref="AskFirstNavigationInterceptor"/> work: arm it, then push the
/// Detail page and pick an outcome from the sheet.
/// </summary>
[ShellMap<InterceptorDemoPage>]
public partial class InterceptorDemoViewModel(
    INavigator navigator,
    NavigationGuardSwitch guards
) : ObservableObject
{
    /// <summary>Arms <see cref="AskFirstNavigationInterceptor"/> - see its comments for the outcomes.</summary>
    public bool AskBeforeDetail
    {
        get => guards.AskBeforeDetail;
        set => this.SetProperty(guards.AskBeforeDetail, value, guards, (g, v) => g.AskBeforeDetail = v);
    }

    [NotifyPropertyChangedFor(nameof(HasResult))]
    [ObservableProperty] string? lastResult;
    public bool HasResult => !string.IsNullOrWhiteSpace(this.LastResult);

    /// <summary>
    /// The result is the point: false means an interceptor cancelled, so the caller knows its
    /// await did nothing. A redirect returns true - the navigation happened, just somewhere else.
    /// </summary>
    [RelayCommand]
    async Task PushGuarded()
    {
        var navigated = await navigator.NavigateTo<DetailViewModel>(
            x => x.Text = "Pushed past the interceptor"
        );
        this.LastResult = navigated ? "navigated (or redirected)" : "cancelled by an interceptor";
    }

    /// <summary>
    /// The escape hatch: this push never shows the sheet, because a guard's own navigation must
    /// not be guarded again.
    /// </summary>
    [RelayCommand]
    async Task PushBypassing()
    {
        await navigator.NavigateTo<DetailViewModel>(
            x => x.Text = "Bypassed the interceptors",
            bypassInterceptors: true
        );
        this.LastResult = "navigated (interceptors skipped)";
    }

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
