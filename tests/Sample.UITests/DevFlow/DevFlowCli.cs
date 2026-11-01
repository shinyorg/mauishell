using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sample.UITests.DevFlow;

/// <summary>
/// The read-only bits of the `maui devflow` CLI that run as their own process. Everything that
/// touches the app goes through <see cref="DevFlowSession"/> instead.
/// </summary>
public static class DevFlowCli
{
    /// <summary>`localhost` can resolve to ::1; the agent listens on IPv4 only.</summary>
    public const string AgentHost = "127.0.0.1";

    /// <summary>
    /// The agent port of THIS app process. Several DevFlow apps are routinely registered with the
    /// broker at once; matching on the pid is the only reliable way to pick ours.
    /// </summary>
    public static async Task<int?> FindAgentPort(int processId)
    {
        var r = await Proc.Run("maui", ["devflow", "list", "--json"], TimeSpan.FromSeconds(20));
        try
        {
            using var doc = JsonDocument.Parse(r.StdOut.Trim() is { Length: > 0 } s ? s : "[]");
            foreach (var a in doc.RootElement.EnumerateArray())
            {
                if (a.TryGetProperty("processId", out var p) && p.ValueKind == JsonValueKind.Number && p.GetInt32() == processId)
                    return a.GetProperty("port").GetInt32();
            }
        }
        catch (JsonException)
        {
            // The broker printed something else (starting up); treat as "not yet".
        }
        return null;
    }
}

/// <summary>
/// One long-lived <c>maui devflow batch</c> process: commands go in on stdin, one JSONL result comes
/// back per command.
/// <para>
/// Why not one CLI process per command: the agent grants a <b>mutation lease</b> to whoever drives
/// it, and every CLI process is a new holder. Back-to-back commands from separate processes fail with
/// "Another DevFlow session is driving this app" until the previous lease lapses. A batch process is
/// a single holder for the whole run. Batch mode only accepts <c>ui</c> and <c>webview</c> commands.
/// </para>
/// </summary>
public sealed class DevFlowSession : IAsyncDisposable
{
    readonly Process process;
    readonly SemaphoreSlim gate = new(1, 1);

    DevFlowSession(Process process, int port)
    {
        this.process = process;
        this.AgentPort = port;
    }

    public int AgentPort { get; }

    public bool IsAlive => !this.process.HasExited;

    public static DevFlowSession Start(int agentPort)
    {
        var psi = new ProcessStartInfo("maui")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = TestConfig.RepoRoot,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var a in new[] { "devflow", "batch", "--continue-on-error", "--delay", "0", "-ah", DevFlowCli.AgentHost, "-ap", agentPort.ToString() })
            psi.ArgumentList.Add(a);

        var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start `maui devflow batch`.");
        p.ErrorDataReceived += (_, _) => { };
        p.BeginErrorReadLine();
        return new DevFlowSession(p, agentPort);
    }

    /// <summary>
    /// Sends one batch line and returns its output. Throws <see cref="DevFlowException"/> on a
    /// non-zero exit code.
    /// </summary>
    public async Task<string> Send(string line, TimeSpan? timeout = null)
    {
        if (line.Contains('\n') || line.Contains(';'))
            throw new ArgumentException("A batch line may not contain newlines or semicolons (they separate commands).", nameof(line));

        var limit = timeout ?? TimeSpan.FromSeconds(60);
        await this.gate.WaitAsync();
        try
        {
            if (this.process.HasExited)
                throw new DevFlowException($"`maui devflow batch` exited ({this.process.ExitCode}).");

            await this.process.StandardInput.WriteLineAsync(line);
            await this.process.StandardInput.FlushAsync();

            using var cts = new CancellationTokenSource(limit);
            string? json;
            try
            {
                json = await this.process.StandardOutput.ReadLineAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // The batch process is now out of step with us; nothing it prints later can be trusted.
                try { this.process.Kill(true); } catch { }
                throw new TimeoutException($"DevFlow did not answer within {limit.TotalSeconds:0}s: {line}");
            }

            if (json is null)
                throw new DevFlowException("`maui devflow batch` closed its output.");

            using var doc = JsonDocument.Parse(json);
            var exit = doc.RootElement.GetProperty("exit_code").GetInt32();
            var output = doc.RootElement.TryGetProperty("output", out var o) ? o.GetString() ?? "" : "";
            if (exit != 0)
                throw new DevFlowException($"{line} failed: {output}");
            return output;
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            this.process.StandardInput.Close();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await this.process.WaitForExitAsync(cts.Token);
        }
        catch
        {
            try { this.process.Kill(true); } catch { }
        }
        this.process.Dispose();
    }
}

public sealed class DevFlowException(string message) : Exception(message);
