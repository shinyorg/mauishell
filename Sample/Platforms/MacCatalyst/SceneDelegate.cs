using Foundation;

namespace Sample;

/// <summary>
/// iOS and macOS 27 terminate an app that has not adopted the UIScene lifecycle, before any managed code runs.
/// Registered by name from UIApplicationSceneManifest in Info.plist.
/// </summary>
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
