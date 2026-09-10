using Microsoft.Extensions.Logging;
using Sample.AI;
using Shiny;
using Shiny.Infrastructure;

namespace Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp
            .CreateBuilder()
            .UseMauiApp<App>()
            .UseShinyControls()
            .UseShinyShell(x => x
                // Every provider and presenter is registered so the bench can switch between them at
                // runtime. Each Use* call registers infrastructure the concrete type needs (the
                // Controls IDialogService, the UXDivers popup host, the presenter options); the
                // switchable pair is registered last, and last registration wins.
                .UseShinyDialogs()
                .UseShinyDialogPresenter()
                .UseUxDiversDialogs()
                .UseUxDiversDialogPresenter()
                .UseDialogs<SwitchableDialogs>()
                .UseDialogPresenter<SwitchableDialogPresenter>()
                .AddGeneratedMaps()
                .AddAiTools()
                // Guards run in registration order on every navigation - route, typed, builder,
                // back, app links, shortcuts, and tab taps.
                .AddNavigationInterceptor<LoggingNavigationInterceptor>()
                .AddNavigationInterceptor<AskFirstNavigationInterceptor>()
            )
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // The concrete providers/presenters the bench routes between - registered by type, since
        // the IDialogs / IDialogPresenter registrations are taken by the switchable pair above.
        builder.Services.AddSingleton<DialogSwitch>();
        builder.Services.AddSingleton<DialogEventLog>();
        builder.Services.AddSingleton<ShellDialogs>();
        builder.Services.AddSingleton<ShinyDialogs>();
        builder.Services.AddSingleton<UxDiversDialogs>();
        builder.Services.AddSingleton<ShellModalDialogPresenter>();
        builder.Services.AddSingleton<ShinyOverlayDialogPresenter>();
        builder.Services.AddSingleton<UxDiversDialogPresenter>();

        builder.Services.AddSingleton<GitHubCopilotAuthService>();
        // ChatView is provider-driven and the provider holds the conversation, so it outlives the page
        builder.Services.AddSingleton<AiChatSessionProvider>();
        builder.Services.AddSingleton<IMauiInitializeService, NavigationLogger>();
        builder.Services.AddSingleton<NavigationGuardSwitch>();

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
