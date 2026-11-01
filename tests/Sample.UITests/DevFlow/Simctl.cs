using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sample.UITests.DevFlow;

/// <summary><c>xcrun simctl</c>, always scoped to one simulator UDID.</summary>
public sealed class Simctl(string udid) : IAppDevice
{
    public string Udid { get; } = udid;

    public string Id => this.Udid;

    public static Task<ProcResult> Run(params string[] args)
        => Proc.Run("xcrun", new[] { "simctl" }.Concat(args), TimeSpan.FromMinutes(3));

    /// <summary>Uninstalling deletes the data container too - the only true "fresh install" on iOS.</summary>
    public async Task Reinstall(string appBundle)
    {
        await this.Terminate();
        await Run("uninstall", this.Udid, TestConfig.BundleId);
        var r = await Run("install", this.Udid, appBundle);
        if (!r.Ok)
            throw new InvalidOperationException($"simctl install failed:\n{r.Combined}");
    }

    public async Task<bool> IsInstalled()
        => (await Run("get_app_container", this.Udid, TestConfig.BundleId)).Ok;

    public Task Terminate() => Run("terminate", this.Udid, TestConfig.BundleId);

    public async Task<int> Launch()
    {
        var r = await Run("launch", "--terminate-running-process", this.Udid, TestConfig.BundleId);
        var m = Regex.Match(r.StdOut, @":\s*(\d+)");
        if (!r.Ok || !m.Success)
            throw new InvalidOperationException($"simctl launch failed:\n{r.Combined}");
        return Int32.Parse(m.Groups[1].Value);
    }

    /// <summary>
    /// The agent registers with the DevFlow broker shortly after launch, on a port the broker hands
    /// out. Matching on the pid proves it is THIS process - other DevFlow apps register too.
    /// </summary>
    public async Task<int> ConnectAgent(int pid)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (await DevFlowCli.FindAgentPort(pid) is { } port)
                return port;

            if (await this.ProcessId() is null)
                throw new InvalidOperationException(
                    $"{TestConfig.BundleId} (pid {pid}) died during launch. Look for Sample-*.ips in ~/Library/Logs/DiagnosticReports.");

            await Task.Delay(1000);
        }
        throw new InvalidOperationException(
            $"No DevFlow agent registered for {TestConfig.BundleId} (pid {pid}) within 90s. Is it a Debug build " +
            "(the agent is only referenced in Debug)? `maui devflow list` shows what did register.");
    }

    /// <summary>The running app's pid (launchctl inside the simulator), or null.</summary>
    public async Task<int?> ProcessId()
    {
        var r = await Run("spawn", this.Udid, "launchctl", "list");
        var line = r.StdOut.Split('\n').FirstOrDefault(l => l.Contains($"UIKitApplication:{TestConfig.BundleId}[", StringComparison.Ordinal));
        return line is not null && Int32.TryParse(line.Split('\t')[0], out var pid) ? pid : null;
    }

    public async Task Screenshot(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Run("io", this.Udid, "screenshot", "--type=png", path);
    }

    // -- device discovery -------------------------------------------------------------------

    public static async Task<Simctl> ResolveOrCreate()
    {
        if (TestConfig.Udid is { } pinned)
            return new Simctl(pinned);

        var r = await Run("list", "devices", "available", "--json");
        using var doc = JsonDocument.Parse(r.StdOut);
        foreach (var runtime in doc.RootElement.GetProperty("devices").EnumerateObject())
        {
            if (!runtime.Name.Contains("iOS", StringComparison.Ordinal))
                continue;
            foreach (var d in runtime.Value.EnumerateArray())
            {
                if (d.GetProperty("name").GetString() == TestConfig.SimulatorName)
                    return new Simctl(d.GetProperty("udid").GetString()!);
            }
        }

        // No runtime given: simctl picks the newest installed iOS runtime.
        var created = await Run("create", TestConfig.SimulatorName, TestConfig.DeviceType);
        if (!created.Ok)
            throw new InvalidOperationException($"Could not create simulator '{TestConfig.SimulatorName}' ({TestConfig.DeviceType}):\n{created.Combined}");
        return new Simctl(created.StdOut.Trim());
    }

    public async Task EnsureBooted()
    {
        await Run("boot", this.Udid);   // fails harmlessly when already booted
        await Proc.Run("xcrun", ["simctl", "bootstatus", this.Udid, "-b"], TimeSpan.FromMinutes(5));
    }
}
