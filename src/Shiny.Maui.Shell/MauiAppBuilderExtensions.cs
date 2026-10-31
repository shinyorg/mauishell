using Shiny.Handlers;

namespace Shiny;


//https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/navigation?view=net-maui-9.0
public static class MauiAppBuilderExtensions
{
    extension(MauiAppBuilder builder)
    {
        public MauiAppBuilder DisableShellFlyoutSwipe()
        {
            DisableShellFlyoutSwipeHandler.Register();
            return builder;
        }
        
        
        public MauiAppBuilder UseShinyShell(Action<ShinyAppBuilder> navBuilderAction)
        {
            var navBuilder = new ShinyAppBuilder(builder);
            navBuilderAction.Invoke(navBuilder);
            navBuilder.RegisterDependencies();
        
            if (!builder.Services.Any(x => x.ImplementationType == typeof(ShinyShellNavigator)))
            {
                builder.Services.AddSingleton(navBuilder);
                builder.Services.TryAddSingleton<IMainThread, MauiMainThread>();
                builder.Services.TryAddSingleton<IDialogs, ShellDialogs>();
                builder.Services.TryAddSingleton<IDialogPresenter, ShellModalDialogPresenter>();
                builder.Services.AddSingleton<ShellTabBadgeManager>();
                builder.Services.AddSingleton<ShellNavigationConfigurator>();
                builder.Services.AddSingleton<NavigationContextAccessor>();
                builder.Services.AddSingleton<INavigationContextAccessor>(
                    sp => sp.GetRequiredService<NavigationContextAccessor>()
                );
                builder.Services.AddSingleton<NavigationInterceptorPipeline>();
                builder.Services.AddSingleton<NavigationObservers>();
                builder.Services.AddSingleton<IRouteRegistry, RouteRegistry>();

                builder.Services.AddSingleton<ShellServices>();
                builder.Services.AddSingleton<ShinyShellNavigator>();
                builder.Services.AddSingleton<INavigator>(
                    sp => sp.GetRequiredService<ShinyShellNavigator>()
                );
                builder.Services.AddSingleton<IMauiInitializeService>(
                    sp => sp.GetRequiredService<ShinyShellNavigator>()
                );
            }
        
            return builder;
        }
    }
}
