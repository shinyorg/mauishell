namespace Sample.UITests.DevFlow;

/// <summary>
/// The simulator or emulator the Sample runs on: <see cref="Simctl"/> (iOS) or <see cref="Adb"/>
/// (Android). Everything above it - the DevFlow session, NativeUi, the tests - is platform neutral.
/// </summary>
public interface IAppDevice
{
    /// <summary>The simulator UDID or the emulator serial.</summary>
    string Id { get; }

    Task EnsureBooted();

    Task<bool> IsInstalled();

    /// <summary>Uninstall + install: a clean data container.</summary>
    Task Reinstall(string appPath);

    Task Terminate();

    /// <summary>Launches the app (killing a running one) and returns its pid.</summary>
    Task<int> Launch();

    /// <summary>Waits for THIS process's DevFlow agent and returns the host port to drive it on.</summary>
    Task<int> ConnectAgent(int pid);

    /// <summary>The running app's pid, or null.</summary>
    Task<int?> ProcessId();

    Task Screenshot(string path);

    static async Task<IAppDevice> ResolveOrCreate() => TestConfig.Platform switch
    {
        TestPlatform.Android => await Adb.Resolve(),
        _ => await Simctl.ResolveOrCreate()
    };
}
