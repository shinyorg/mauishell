using Sample.UITests.Pages;

namespace Sample.UITests.DevFlow;

/// <summary>
/// Owns the simulator or emulator, the app process and the DevFlow session for the whole suite.
/// <para>
/// Created once (see <see cref="AppCollection"/>). After every test <see cref="Recover"/> returns the
/// app to the home list rather than reinstalling - a relaunch costs a cold start each time, so it is
/// only the fallback.
/// </para>
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    public IAppDevice Device { get; private set; } = null!;
    public NativeUi Ui { get; private set; } = null!;
    public int ProcessId { get; private set; }

    DevFlowSession? session;
    bool relaunchRequested;

    public async ValueTask InitializeAsync()
    {
        this.Device = await IAppDevice.ResolveOrCreate();
        await this.Device.EnsureBooted();

        if (TestConfig.BuildBeforeRun)
            await BuildApp();

        if (TestConfig.InstallBeforeRun)
        {
            if (!Directory.Exists(TestConfig.AppPath) && !File.Exists(TestConfig.AppPath))
                throw new InvalidOperationException($"No app at {TestConfig.AppPath}. Run tests/Sample.UITests/{RunScript}, or set SAMPLE_UITEST_BUILD=true.");
            await this.Device.Reinstall(TestConfig.AppPath);
        }
        else if (!await this.Device.IsInstalled())
        {
            throw new InvalidOperationException(
                $"{TestConfig.BundleId} is not installed on {this.Device.Id}. Run tests/Sample.UITests/{RunScript} (builds, installs and runs), or set SAMPLE_UITEST_BUILD=true.");
        }

        await this.Relaunch();
    }

    /// <summary>Kills and relaunches the app, reconnects DevFlow, and waits for the home list.</summary>
    public async Task Relaunch()
    {
        if (this.session is not null)
        {
            await this.session.DisposeAsync();
            this.session = null;
        }

        this.ProcessId = await this.Device.Launch();
        var port = await this.Device.ConnectAgent(this.ProcessId);
        this.session = DevFlowSession.Start(port);
        this.Ui = new NativeUi(this.session);
        await this.Ui.WaitForPage("HomePage", TimeSpan.FromSeconds(60));

        // Every IDialogs call in the Sample goes through the bench's provider choice. Pick the one
        // DevFlow can see (the choice is persisted, but a fresh install starts on UXDivers).
        await new DialogBench(this.Ui).Use(DialogProviderKind.ShinyControls, DialogPresenterKind.ShellModal);
        await this.Ui.Navigate("//HomePage");
        await this.Ui.WaitForPageGone("DialogDemoPage");
    }

    /// <summary>
    /// For a test that changes app state Recover cannot see - an armed interceptor, a swapped Shell -
    /// so the next test starts from a cold launch.
    /// </summary>
    public void RequestRelaunch() => this.relaunchRequested = true;

    /// <summary>
    /// After every test: the standard Shell, on the home list, nothing pushed, no modal or dialog.
    /// A dead session, a guard that blocks going home or a left-over page all mean a relaunch.
    /// </summary>
    public async Task Recover()
    {
        try
        {
            if (this.relaunchRequested || this.session is null || !this.session.IsAlive)
                throw new DevFlowException("relaunch");

            // Short: a page whose guard puts up a dialog blocks this, and a relaunch is quicker than waiting
            await this.Ui.Navigate("//HomePage", TimeSpan.FromSeconds(10));
            await this.Ui.WaitUntil(this.IsHome, "the home list with nothing on top of it", TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            this.relaunchRequested = false;
            await this.Relaunch();
        }
    }

    async Task<bool> IsHome()
    {
        if (await this.Ui.Route() != "//HomePage" || !await this.Ui.Exists("AppShell"))
            return false;

        // Type matching in DevFlow is exact (a "ContentPage" query finds no HomePage), so the
        // Sample's pages are listed by name.
        foreach (var page in PagesOtherThanHome)
        {
            if (await this.Ui.Exists(page))
                return false;
        }
        return true;
    }

    static readonly string[] PagesOtherThanHome =
    [
        "NavigationDemoPage", "DetailPage", "XamlNavDemoPage", "BuilderDemoPage", "ModalDemoPage",
        "InterceptorDemoPage", "DialogDemoPage", "OverlayHostDemoPage", "BadgeDemoPage",
        "LifecycleDemoPage", "PickColorPage", "LongTextPage", "ShellTestPage"
    ];

    static string RunScript => TestConfig.Platform == TestPlatform.Android ? "run-android.sh" : "run-ios.sh";

    public static async Task BuildApp()
    {
        // Same as the run scripts: its own output folders, so it never fights another build of the Sample.
        string[] platform = TestConfig.Platform == TestPlatform.Android
            ?
            [
                "-f", "net10.0-android", "-p:RuntimeIdentifier=android-arm64",
                // A Debug build normally leaves the assemblies out for fast deployment; an installed apk needs them
                "-p:EmbedAssembliesIntoApk=true", "-p:AndroidPackageFormat=apk"
            ]
            : ["-f", "net10.0-ios", "-p:RuntimeIdentifier=iossimulator-arm64"];

        var result = await Proc.Run("dotnet",
            [
                "build", TestConfig.AppProject, "-c", "Debug",
                .. platform,
                $"-p:OutputPath={TestConfig.AppBuildDirFromSample}/bin/",
                $"-p:IntermediateOutputPath={TestConfig.AppBuildDirFromSample}/obj/",
                "-nologo", "-v:q"
            ],
            TimeSpan.FromMinutes(20));
        if (!result.Ok)
            throw new InvalidOperationException($"Building the Sample failed:\n{result.Combined}");
    }

    public async ValueTask DisposeAsync()
    {
        if (this.session is not null)
            await this.session.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "app";
}
