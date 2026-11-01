namespace Sample.UITests.DevFlow;

public enum TestPlatform { iOS, Android }

/// <summary>
/// Everything the suite needs to find the simulator or emulator, the app and its DevFlow agent. Every value can
/// be overridden by an environment variable so CI (or the run scripts) can point the same tests elsewhere.
/// </summary>
public static class TestConfig
{
    public const string BundleId = "org.shiny.maui";

    /// <summary>SAMPLE_UITEST_PLATFORM: <c>ios</c> (default) or <c>android</c>.</summary>
    public static TestPlatform Platform { get; } = Env("SAMPLE_UITEST_PLATFORM", "ios").ToLowerInvariant() switch
    {
        "ios" => TestPlatform.iOS,
        "android" => TestPlatform.Android,
        var other => throw new InvalidOperationException($"SAMPLE_UITEST_PLATFORM must be ios or android, not '{other}'.")
    };

    /// <summary>
    /// The simulator the suite runs on. A dedicated one by default: the everyday simulator usually
    /// has someone's own build of the Sample on it, and two installs of one bundle id replace each other.
    /// Created from <see cref="DeviceType"/> when missing.
    /// </summary>
    public static string SimulatorName => Env("SAMPLE_UITEST_SIM", "MauiShell-UITests");

    /// <summary>Pin to one simulator by UDID (wins over <see cref="SimulatorName"/>).</summary>
    public static string? Udid => Environment.GetEnvironmentVariable("SAMPLE_UITEST_UDID") is { Length: > 0 } u ? u : null;

    public static string DeviceType => Env("SAMPLE_UITEST_DEVICE_TYPE", "iPhone 18 Pro");

    /// <summary>The Android emulator the suite runs on - dedicated, for the same reason as the simulator.</summary>
    public static string AndroidAvd => Env("SAMPLE_UITEST_AVD", "MauiShell-UITests");

    /// <summary>Pin to one Android device by serial (wins over <see cref="AndroidAvd"/>).</summary>
    public static string? AndroidSerial => Environment.GetEnvironmentVariable("SAMPLE_UITEST_ANDROID_SERIAL") is { Length: > 0 } s ? s : null;

    /// <summary>ANDROID_HOME, else the SDK where Android Studio puts it on macOS.</summary>
    public static string AndroidSdk => Env("ANDROID_HOME",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Android", "sdk"));

    public static string AdbPath => Path.Combine(AndroidSdk, "platform-tools", "adb");

    public static string EmulatorPath => Path.Combine(AndroidSdk, "emulator", "emulator");

    /// <summary>Repo root, found by walking up for the solution file.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string AppProject => Path.Combine(RepoRoot, "Sample", "Sample.csproj");

    /// <summary>
    /// The suite's own build of the Sample (run-ios.sh / run-android.sh / SAMPLE_UITEST_BUILD=true put it
    /// here). Relative to the Sample project, as OutputPath wants it.
    /// </summary>
    public static string AppBuildDirFromSample => Platform == TestPlatform.Android
        ? "../tests/Sample.UITests/.app-build-android"
        : "../tests/Sample.UITests/.app-build";

    public static string AppBuildDir => Path.GetFullPath(Path.Combine(RepoRoot, "Sample", AppBuildDirFromSample));

    /// <summary>The .app bundle (iOS) or signed .apk (Android) that gets installed.</summary>
    public static string AppPath => Env("SAMPLE_UITEST_APP", Platform == TestPlatform.Android
        ? Path.Combine(AppBuildDir, "bin", $"{BundleId}-Signed.apk")
        : Path.Combine(AppBuildDir, "bin", "Sample.app"));

    /// <summary>When true the fixture builds the Sample (Debug, with the DevFlow agent) and reinstalls it.</summary>
    public static bool BuildBeforeRun => Flag("SAMPLE_UITEST_BUILD");

    /// <summary>When true the fixture uninstalls + installs <see cref="AppPath"/> (a clean data container).</summary>
    public static bool InstallBeforeRun => BuildBeforeRun || Flag("SAMPLE_UITEST_INSTALL");

    /// <summary>Where failure screenshots and visual trees land.</summary>
    public static string ArtifactDir => Env("SAMPLE_UITEST_ARTIFACTS", Path.Combine(RepoRoot, "artifacts", "uitests"));

    static bool Flag(string name)
        => String.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase)
           || Environment.GetEnvironmentVariable(name) == "1";

    static string Env(string name, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return String.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MauiShell.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate the repo root (no MauiShell.slnx found above the test binaries).");
    }
}
