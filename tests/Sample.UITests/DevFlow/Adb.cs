using System.Diagnostics;

namespace Sample.UITests.DevFlow;

/// <summary>
/// <c>adb</c>, always scoped to one emulator serial.
/// <para>
/// The DevFlow agent inside the emulator listens on <see cref="AgentDevicePort"/> and does not
/// register with the broker on the host (it cannot reach it), so <c>maui devflow list</c> never shows
/// it. The suite forwards a host port to it instead and drives that.
/// </para>
/// </summary>
public sealed class Adb(string serial) : IAppDevice
{
    /// <summary>The agent's default port when it has no broker to hand one out.</summary>
    public const int AgentDevicePort = 9223;

    int? forwardedPort;
    string? launchComponent;

    public string Id { get; } = serial;

    public static Task<ProcResult> Run(params string[] args)
        => Proc.Run(TestConfig.AdbPath, args, TimeSpan.FromMinutes(3));

    Task<ProcResult> Device(params string[] args)
        => Run(["-s", this.Id, .. args]);

    public async Task Reinstall(string apk)
    {
        await this.Terminate();
        await this.Device("uninstall", TestConfig.BundleId);
        var r = await this.Device("install", apk);
        if (!r.Ok)
            throw new InvalidOperationException($"adb install failed:\n{r.Combined}");
    }

    public async Task<bool> IsInstalled()
        => (await this.Device("shell", "pm", "path", TestConfig.BundleId)).StdOut.Contains("package:", StringComparison.Ordinal);

    public Task Terminate() => this.Device("shell", "am", "force-stop", TestConfig.BundleId);

    public async Task<int> Launch()
    {
        // The MainActivity name is a generated crc64 hash, so ask the package manager for it
        this.launchComponent ??= await this.ResolveLaunchComponent();

        // -S stops a running instance first, -W waits until the activity has started
        var r = await this.Device("shell", "am", "start", "-S", "-W", "-n", this.launchComponent);
        if (!r.Ok || r.StdOut.Contains("Error", StringComparison.Ordinal))
            throw new InvalidOperationException($"am start failed:\n{r.Combined}");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await this.ProcessId() is { } pid)
                return pid;
            await Task.Delay(500);
        }
        throw new InvalidOperationException($"{TestConfig.BundleId} started but no process appeared. `adb logcat` has the reason.");
    }

    async Task<string> ResolveLaunchComponent()
    {
        var r = await this.Device("shell", "cmd", "package", "resolve-activity", "--brief", "-c", "android.intent.category.LAUNCHER", TestConfig.BundleId);
        var component = r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (component is null || !component.StartsWith(TestConfig.BundleId + "/", StringComparison.Ordinal))
            throw new InvalidOperationException($"Could not find the launcher activity of {TestConfig.BundleId}:\n{r.Combined}");
        return component;
    }

    /// <summary>
    /// The agent starts a moment after the activity. It is THIS process because the forwarded port
    /// can only reach this emulator, and only one copy of the app runs on it.
    /// </summary>
    public async Task<int> ConnectAgent(int pid)
    {
        this.forwardedPort ??= await this.Forward();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            var status = await Proc.Run("maui",
                ["devflow", "ui", "status", "--json", "-ah", DevFlowCli.AgentHost, "-ap", this.forwardedPort.Value.ToString()],
                TimeSpan.FromSeconds(20));
            if (status.Ok)
                return this.forwardedPort.Value;

            if (await this.ProcessId() != pid)
                throw new InvalidOperationException(
                    $"{TestConfig.BundleId} (pid {pid}) died during launch. `adb -s {this.Id} logcat -d | grep -A20 FATAL` has the reason.");

            await Task.Delay(1000);
        }
        throw new InvalidOperationException(
            $"No DevFlow agent answered on device port {AgentDevicePort} for {TestConfig.BundleId} (pid {pid}) within 90s. " +
            "Is it a Debug build (the agent is only referenced in Debug)?");
    }

    async Task<int> Forward()
    {
        // tcp:0 lets adb pick a free host port and print it
        var r = await this.Device("forward", "tcp:0", $"tcp:{AgentDevicePort}");
        if (!r.Ok || !Int32.TryParse(r.StdOut.Trim(), out var port))
            throw new InvalidOperationException($"adb forward failed:\n{r.Combined}");
        return port;
    }

    public async Task<int?> ProcessId()
    {
        var r = await this.Device("shell", "pidof", TestConfig.BundleId);
        return Int32.TryParse(r.StdOut.Trim().Split(' ')[0], out var pid) ? pid : null;
    }

    public async Task Screenshot(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string onDevice = "/data/local/tmp/uitest-screen.png";
        await this.Device("shell", "screencap", "-p", onDevice);
        await this.Device("pull", onDevice, path);
    }

    // -- device discovery -------------------------------------------------------------------

    /// <summary>
    /// <see cref="TestConfig.AndroidSerial"/> when set, else a running emulator of
    /// <see cref="TestConfig.AndroidAvd"/>, else that AVD booted. Creating the AVD is run-android.sh's job.
    /// </summary>
    public static async Task<Adb> Resolve()
    {
        if (TestConfig.AndroidSerial is { } pinned)
            return new Adb(pinned);

        if (await FindRunningAvd(TestConfig.AndroidAvd) is { } running)
            return new Adb(running);

        // Detached: the emulator outlives the test run, like a booted simulator does
        Process.Start(new ProcessStartInfo(TestConfig.EmulatorPath)
        {
            ArgumentList = { "-avd", TestConfig.AndroidAvd, "-no-snapshot-save", "-no-boot-anim", "-no-audio" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            if (await FindRunningAvd(TestConfig.AndroidAvd) is { } started)
                return new Adb(started);
            await Task.Delay(2000);
        }
        throw new InvalidOperationException(
            $"Emulator '{TestConfig.AndroidAvd}' did not start. Run tests/Sample.UITests/run-android.sh (it creates the AVD), or set SAMPLE_UITEST_ANDROID_SERIAL.");
    }

    static async Task<string?> FindRunningAvd(string avd)
    {
        var devices = await Run("devices");
        foreach (var line in devices.StdOut.Split('\n').Skip(1))
        {
            var parts = line.Split('\t', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !parts[0].StartsWith("emulator-", StringComparison.Ordinal))
                continue;

            // Prints the AVD name, then "OK"
            var name = await Run("-s", parts[0], "emu", "avd", "name");
            if (name.StdOut.Split('\n', StringSplitOptions.TrimEntries).FirstOrDefault() == avd)
                return parts[0];
        }
        return null;
    }

    public async Task EnsureBooted()
    {
        await this.Device("wait-for-device");
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            if ((await this.Device("shell", "getprop", "sys.boot_completed")).StdOut.Trim() == "1")
                return;
            await Task.Delay(2000);
        }
        throw new InvalidOperationException($"{this.Id} did not finish booting within 5 minutes.");
    }
}
