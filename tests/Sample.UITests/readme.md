# Sample UI tests

End-to-end tests that drive the real **Sample** app on an iOS simulator through
[MAUI DevFlow](https://www.nuget.org/packages/Microsoft.Maui.DevFlow.Agent): navigation, modals, the
builder, XAML navigation, interceptors, lifecycle, every dialog provider/presenter that DevFlow can
see, shell switching and tab badges.

```bash
tests/Sample.UITests/run-ios.sh                          # build + fresh install + run (~3.5 minutes after the build)
tests/Sample.UITests/run-ios.sh --no-build               # reuse the last build
tests/Sample.UITests/run-ios.sh --filter DialogTests     # any `dotnet test --filter` expression
```

`run-ios.sh`:

1. Checks the `maui` CLI against the agent version in `Directory.Packages.props`.
2. Creates or boots the **dedicated** simulator `MauiShell-UITests` (iPhone 18 Pro), so it never replaces
   the Sample you have installed on your everyday simulator.
3. Builds the Sample in Debug into `tests/Sample.UITests/.app-build`, so it never fights an IDE build over
   `Sample/bin|obj`.
4. Uninstalls and reinstalls the app (a clean data container).
5. Runs `dotnet test`. The fixture launches the app itself.

From an IDE or a plain `dotnet test`, the app has to be installed on the simulator already, or set
`SAMPLE_UITEST_INSTALL=true` (reinstall the last build) or `SAMPLE_UITEST_BUILD=true` (build it too).
`dotnet test MauiShell.slnx` includes this project, so it needs the simulator as well. CI builds
`Build.slnf`, which leaves it out.

## Requirements

- **The `maui` CLI**: `dotnet tool install -g Microsoft.Maui.Cli --prerelease`, on the same version as
  `Microsoft.Maui.DevFlow.Agent` in `Directory.Packages.props` (run-ios.sh warns if they differ).
- **Xcode with an iOS simulator runtime.**

## How it works

```
 test process (net10.0, xunit v3)                     iOS simulator
 ─────────────────────────────────                    ─────────────────────────────────────────
 Simctl ── xcrun simctl install/launch ─────────────▶ Sample (Debug)
 DevFlowCli ── maui devflow list (find agent by pid)   └─ MAUI DevFlow agent :102xx
 DevFlowSession ── maui devflow batch (stdin/JSONL) ─▶       ui query / tap / fill / set-property / status
 NativeUi ── exact-match queries, waits, taps                (the MAUI visual tree)
```

- **Agent.** Debug builds of the Sample reference `Microsoft.Maui.DevFlow.Agent` and call
  `AddMauiDevFlowAgent()` in `MauiProgram`. The port comes from the DevFlow broker, so the fixture finds
  it with `maui devflow list` **by the pid `simctl launch` returned**. Other DevFlow apps are registered
  too.
- **One batch process, one lease.** The agent hands a mutation lease to whoever drives it, and every
  `maui devflow` process is a new holder: separate CLI calls in quick succession fail with "Another
  DevFlow session is driving this app". The suite keeps one `maui devflow batch` for the whole run.
  Batch mode accepts only `ui`/`webview` commands, so `storage` (preferences) is off limits.
- **Dialogs go through ShinyControls.** Every `IDialogs` call in the Sample routes through the Dialog
  Bench's provider choice. The fixture selects **ShinyControls** through the bench's own Pickers after
  every launch, because it is the only provider DevFlow can see (see below).
- **Known state.** After every test, pass or fail, `Recover()` navigates to `//HomePage` and checks
  that only the home list is left. A dead session, a guard that blocks going home, a swapped Shell or a
  test that called `RequestRelaunch()` (armed interceptor, Lifecycle's guard) means a relaunch.
- **Failure evidence** goes to `artifacts/uitests/<test>/`: `screen.png`, `visual-tree.json` and
  `state.txt` (route, visible labels and buttons).

### DevFlow traps (all found against this app)

| Trap | What the suite does |
|---|---|
| `--text` matching is a **substring** match: `ui tap --text "Cancel"` hits "Cancelled by the caller after 3s" | `NativeUi` queries, picks the **exact** text, taps by element id |
| Taps are logical (the command or gesture recognizer is invoked), not touches: a CollectionView row cannot be selected | The Sample's home rows use a `TapGestureRecognizer`, with the row title as `AutomationId` |
| An overlay does not block a tap on what is under it | Tests never rely on a dialog being modal |
| Covered pages stay in the tree (listed newest first), and type matching is exact (`ContentPage` finds no `HomePage`) | Wait for the expected page *and* for the left one to be gone; `IsHome` lists the Sample's pages by name |
| Native alerts (Shell provider) are invisible to `ui alert` on the iOS 27 simulator; UXDivers popups live outside the MAUI tree | ShinyControls provider only |
| `ui navigate` waits for Shell to finish, so a guard that shows a dialog blocks it - and the batch session - until the agent gives up, after which the tree reader reports "UI kept changing" | Used only for resets, with a short timeout and a relaunch as fallback |
| A batch line ends at a newline or `;`, and quotes inside arguments break parsing | `NativeUi` rejects them; selectors carry no attribute filters |

## Layout

```
DevFlow/   the driver: Proc, Simctl, DevFlowCli + DevFlowSession (batch), NativeUi, AppFixture, UiTest, TestConfig
Pages/     page objects: HomeList, DialogBench (+ provider/presenter switching), Dialogs (answering a ShinyControls dialog)
Flows/     the tests
run-ios.sh build + install + run
```

## What is covered

| Test class | Proves |
|---|---|
| `SmokeTests` | Cold start lands on the standard Shell's home list; every home row opens its page |
| `NavigationTests` | Push by route / by ViewModel pass args as query parameters; the configure overload sets the ViewModel; `GoBack` args reach the previous page; `OnNavigatingFrom` can add a back arg; `PopToRoot` clears a two-deep stack; `INavigationConfirmation` is **not** asked for the app's own `GoBack` |
| `XamlNavigationTests` | `Navigate.Route` with a bound parameter, with none, and `RelativeNavigation=False` resetting the stack to `//HomePage/DetailPage` |
| `BuilderTests` | A three-segment chain lands in order and unwinds one at a time; a chain ending in a modal; pop + push replaces the page |
| `ModalTests` | Default args, push inside a modal and back, close; a ViewModel dialog raised from a modal (ShellModal and ShinyOverlay) returns its result to the modal |
| `DialogTests` | Alert, Confirm (yes/no), Prompt (text/cancel), ActionSheet; ViewModel dialogs under ShellModal and ShinyOverlay: typed result, `IsCancelled`, lifecycle + `Dispose`, nested dialog, caller's token → `OperationCanceledException` and teardown; navigating away under an overlay dialog completes the await as cancelled; `GoBack` under a ShellModal dialog closes the dialog; ShinyContentPage's `OverlayHost`, also over its loading overlay |
| `InterceptorTests` | Continue, Redirect (destination never built, `NavigateTo` true), Cancel (`NavigateTo` false); the interceptor sees the destination ViewModel already configured; `bypassInterceptors` skips it |
| `LifecycleTests` | `OnAppearing`; push and return runs `OnNavigatingFrom` → `OnDisappearing` → `OnAppearing`, without `CanNavigate` |
| `ShellSwitchingTests` | `SwitchShell` to Tabbed and Flyout, navigate inside the new Shell, and back to the standard one |
| `TabBadgeTests` | Declining the Shell switch sets nothing; set 5 → 99 → clear, read back through `ShellTabBadgeManager` |

## Not covered (yet)

- **Android.** iOS only for now; the driver above `Simctl` is platform neutral.
- **User-driven navigation guards.** `INavigationConfirmation` guards the back button, tabs and flyout.
  DevFlow cannot touch the native back button, and `ui navigate ..` blocks the agent while the confirm
  dialog waits (see the traps above).
- **The Shell and UXDivers dialog providers, and the UxDiversPopup presenter** - not visible to DevFlow.
- **App links, app shortcuts, the AI chat page.**
