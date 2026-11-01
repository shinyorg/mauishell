using Sample.UITests.DevFlow;

namespace Sample.UITests.Pages;

/// <summary>
/// A ShinyControls dialog (the provider the fixture selects): an overlay of ordinary MAUI views,
/// found by its message Label and answered by tapping one of its Buttons.
/// </summary>
public sealed class Dialogs(NativeUi ui)
{
    /// <summary>Waits for a dialog showing <paramref name="message"/> (any Label, anywhere in the tree).</summary>
    public Task WaitFor(string message, TimeSpan? timeout = null)
        => ui.WaitUntil(() => this.IsShowing(message), $"a dialog saying '{message}'", timeout);

    public Task WaitUntilGone(string message)
        => ui.WaitUntil(async () => !await this.IsShowing(message), $"the dialog saying '{message}' to close");

    public async Task<bool> IsShowing(string message)
        => (await ui.Query(type: "Label")).Any(l => l.IsVisible && l.Text == message);

    /// <summary>Waits for the dialog, taps <paramref name="button"/>, waits for it to close.</summary>
    public async Task Answer(string message, string button)
    {
        await this.WaitFor(message);
        await ui.TapButton(button);
        await this.WaitUntilGone(message);
    }
}
