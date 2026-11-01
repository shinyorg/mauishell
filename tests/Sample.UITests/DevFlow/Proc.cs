using System.Diagnostics;
using System.Text;

namespace Sample.UITests.DevFlow;

public sealed record ProcResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Ok => this.ExitCode == 0;

    public string Combined => String.IsNullOrWhiteSpace(this.StdErr)
        ? this.StdOut
        : $"{this.StdOut}\n{this.StdErr}";
}

/// <summary>Minimal async process runner with a hard timeout, shared by the simctl and CLI wrappers.</summary>
public static class Proc
{
    public static async Task<ProcResult> Run(
        string fileName,
        IEnumerable<string> args,
        TimeSpan? timeout = null,
        IDictionary<string, string>? env = null
    )
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = TestConfig.RepoRoot
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        if (env is not null)
        {
            foreach (var (k, v) in env)
                psi.Environment[k] = v;
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start '{fileName}'.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var limit = timeout ?? TimeSpan.FromSeconds(60);
        using var cts = new CancellationTokenSource(limit);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"'{fileName} {String.Join(' ', args)}' did not finish within {limit.TotalSeconds:0}s.");
        }

        return new ProcResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
