namespace Sample.UITests.DevFlow;

/// <summary>
/// Base for every UI test. On failure it writes a screenshot and the MAUI visual tree to
/// the artifacts folder. After every test, pass or fail, the app is returned to the home list
/// (<see cref="AppFixture.Recover"/>).
/// </summary>
[Collection(AppCollection.Name)]
public abstract class UiTest(AppFixture app) : IAsyncLifetime
{
    protected AppFixture App { get; } = app;

    /// <summary>Always the current connection - it is replaced when the app is relaunched.</summary>
    protected NativeUi Ui => this.App.Ui;

    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (TestContext.Current.TestState?.Result is TestResult.Failed)
            await this.CaptureFailureArtifacts(TestContext.Current.Test?.TestDisplayName ?? "unknown");

        await this.App.Recover();
        GC.SuppressFinalize(this);
    }

    async Task CaptureFailureArtifacts(string testName)
    {
        try
        {
            var safe = String.Concat(testName.Split(Path.GetInvalidFileNameChars()));
            var dir = Path.Combine(TestConfig.ArtifactDir, safe);
            Directory.CreateDirectory(dir);

            await this.App.Device.Screenshot(Path.Combine(dir, "screen.png"));
            try { await File.WriteAllTextAsync(Path.Combine(dir, "visual-tree.json"), await this.Ui.TreeDump()); } catch { }
            try { await File.WriteAllTextAsync(Path.Combine(dir, "state.txt"), await this.Ui.Describe()); } catch { }
            TestContext.Current.SendDiagnosticMessage($"[uitests] failure artifacts: {dir}");
        }
        catch (Exception ex)
        {
            // Never let diagnostics turn a clean assertion failure into a confusing one.
            Console.WriteLine($"[uitests] could not capture failure artifacts: {ex.Message}");
        }
    }
}
