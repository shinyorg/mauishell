#if !ANDROID && !IOS && !MACCATALYST
namespace Shiny.Infrastructure;

public static partial class AppLinkLifecycle
{
    // Windows and net11.0 have no automatic hook - forward to IAppLinks.Handle by hand.
    public static partial void Register(MauiAppBuilder builder) { }
}
#endif
