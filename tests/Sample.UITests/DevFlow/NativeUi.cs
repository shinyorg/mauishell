using System.Text.Json;

namespace Sample.UITests.DevFlow;

public sealed record UiElement(string Id, string Type, string? Text, string? AutomationId, bool IsVisible);

/// <summary>
/// The Sample's MAUI visual tree, driven through the DevFlow agent's <c>ui</c> commands.
/// <para>
/// Traps baked in here, all found against the real app:
/// </para>
/// <list type="bullet">
/// <item><b>Text matching in the CLI is a substring match</b> - <c>ui tap --text "Cancel"</c> hits
/// "Cancelled by the caller after 3s". Everything here queries, picks the <i>exact</i> text in C#,
/// then acts on that element's id.</item>
/// <item><b>Taps are logical, not touches.</b> DevFlow invokes the element's command or gesture
/// recognizer. A Label with no recognizer cannot be tapped, CollectionView selection cannot be
/// driven (so the Sample's home rows use a TapGestureRecognizer), and an overlay does not block a
/// tap on what is underneath it - a test must not rely on a dialog being modal.</item>
/// <item><b>A covered page is still in the tree.</b> HomePage matches a query while Detail is pushed
/// on top of it (stacked pages are listed newest first). Wait for the page you expect to appear and
/// for the one you left to be gone.</item>
/// <item><b>Type matching is exact.</b> A <c>ContentPage</c> query finds no HomePage.</item>
/// <item><b>Only the ShinyControls dialogs are reachable.</b> Native alerts (the Shell provider)
/// are invisible to <c>ui alert</c> on the iOS 27 simulator, and UXDivers popups live outside the
/// MAUI tree. See <see cref="Pages.DialogBench"/>.</item>
/// </list>
/// </summary>
public sealed class NativeUi(DevFlowSession session)
{
    public DevFlowSession Session { get; } = session;

    static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(15);

    /// <summary>
    /// One command. A lease or busy reply right after a (re)launch is retried: the agent says itself
    /// that the previous holder is still letting go.
    /// </summary>
    public async Task<string> Send(string line, TimeSpan? timeout = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await this.Session.Send(line, timeout);
            }
            catch (DevFlowException ex) when (attempt < 10 && IsTransient(ex))
            {
                await Task.Delay(1000 * Math.Min(attempt, 3));
            }
        }
    }

    static bool IsTransient(DevFlowException ex)
        => ex.Message.Contains("Another DevFlow session", StringComparison.Ordinal)
           || ex.Message.Contains("ui-mutation-busy", StringComparison.Ordinal)
           || ex.Message.Contains("The UI kept changing", StringComparison.Ordinal);

    // -- reading the tree ------------------------------------------------------------------------

    /// <summary><c>ui query</c> with any of type / automationId / CSS-ish selector (no quotes inside it).</summary>
    public async Task<IReadOnlyList<UiElement>> Query(string? type = null, string? automationId = null, string? selector = null)
    {
        var line = "ui query --fields id,type,text,automationId,isVisible";
        if (type is not null)
            line += $" --type {type}";
        if (automationId is not null)
            line += $" --automationId {Quote(automationId)}";
        if (selector is not null)
            line += $" --selector {Quote(selector)}";

        var output = await this.Send(line);
        using var doc = JsonDocument.Parse(output.Trim() is { Length: > 0 } s ? s : "[]");
        return doc.RootElement.EnumerateArray().Select(e => new UiElement(
            e.GetProperty("id").GetString()!,
            e.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
            e.TryGetProperty("text", out var x) ? x.GetString() : null,
            e.TryGetProperty("automationId", out var a) ? a.GetString() : null,
            !e.TryGetProperty("isVisible", out var v) || v.GetBoolean()
        )).ToList();
    }

    /// <summary>Is a page (or any element) of this type in the tree?</summary>
    public async Task<bool> Exists(string type) => (await this.Query(type: type)).Count > 0;

    /// <summary>The text of every Label under the first <paramref name="pageType"/> in the tree.</summary>
    public async Task<IReadOnlyList<string>> Labels(string pageType)
        => (await this.Query(selector: $"{pageType} Label"))
            .Select(e => e.Text)
            .OfType<string>()
            .ToList();

    /// <summary>The Shell route the agent reports, e.g. <c>//HomePage/NavigationDemoPage</c> (modals are not in it).</summary>
    public async Task<string> Route()
    {
        using var doc = JsonDocument.Parse(await this.Send("ui status"));
        return doc.RootElement.TryGetProperty("route", out var r) ? r.GetString() ?? "" : "";
    }

    public async Task<string> Property(string elementId, string name)
    {
        using var doc = JsonDocument.Parse(await this.Send($"ui property {elementId} {name}"));
        var value = doc.RootElement.GetProperty("value");
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ToString();
    }

    public async Task<string> TreeDump() => await this.Send("ui tree --format compact");

    // -- waiting ---------------------------------------------------------------------------------

    /// <summary>Polls until <paramref name="condition"/> holds. On timeout, says what the app was showing.</summary>
    public async Task WaitUntil(Func<Task<bool>> condition, string description, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultWait;
        var deadline = DateTime.UtcNow + limit;
        while (true)
        {
            if (await condition())
                return;

            if (DateTime.UtcNow >= deadline)
                throw new DevFlowException($"Timed out after {limit.TotalSeconds:0}s waiting for {description}.\n{await this.Describe()}");

            await Task.Delay(250);
        }
    }

    public Task WaitForPage(string pageType, TimeSpan? timeout = null)
        => this.WaitUntil(() => this.Exists(pageType), $"page {pageType}", timeout);

    public Task WaitForPageGone(string pageType, TimeSpan? timeout = null)
        => this.WaitUntil(async () => !await this.Exists(pageType), $"page {pageType} to go away", timeout);

    /// <summary>Waits for a Label under <paramref name="pageType"/> whose text is exactly <paramref name="text"/>.</summary>
    public Task WaitForLabel(string pageType, string text, TimeSpan? timeout = null)
        => this.WaitUntil(async () => (await this.Labels(pageType)).Contains(text), $"label '{text}' on {pageType}", timeout);

    /// <summary>Waits for a Label under <paramref name="pageType"/> starting with <paramref name="prefix"/>, and returns its text.</summary>
    public async Task<string> WaitForLabelStartingWith(string pageType, string prefix, TimeSpan? timeout = null)
    {
        string? found = null;
        await this.WaitUntil(
            async () => (found = (await this.Labels(pageType)).FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal))) is not null,
            $"a label starting '{prefix}' on {pageType}",
            timeout
        );
        return found!;
    }

    // -- acting ----------------------------------------------------------------------------------

    /// <summary>
    /// Taps the visible Button whose text is exactly <paramref name="text"/>. When several match the
    /// last is taken - where an overlay dialog's buttons sit. Stacked pages list newest first, but
    /// the Sample's navigation buttons act on the stack, not on the page they are on.
    /// </summary>
    public async Task TapButton(string text, TimeSpan? timeout = null)
    {
        UiElement? button = null;
        await this.WaitUntil(
            async () => (button = (await this.Query(type: "Button")).LastOrDefault(b => b.IsVisible && b.Text == text)) is not null,
            $"a button '{text}'",
            timeout
        );
        await this.Send($"ui tap {button!.Id}");
        await Task.Delay(250);
    }

    /// <summary>Invokes the tap gesture recognizer on the element with this AutomationId (e.g. a home row).</summary>
    public async Task TapGesture(string automationId)
    {
        await this.WaitUntil(async () => (await this.Query(automationId: automationId)).Count > 0, $"'{automationId}'");
        await this.Send($"ui gesture tap --automationId {Quote(automationId)}");
        await Task.Delay(250);
    }

    /// <summary>Replaces the text of the <paramref name="index"/>th Entry under <paramref name="scope"/>.</summary>
    public async Task Fill(string scope, string text, int index = 0)
    {
        UiElement? entry = null;
        await this.WaitUntil(
            async () => (entry = (await this.Query(selector: $"{scope} Entry")).ElementAtOrDefault(index)) is not null,
            $"entry #{index} on {scope}"
        );
        await this.Send($"ui fill {entry!.Id} {Quote(text)}");
    }

    /// <summary>Sets a bindable property the way the control itself would (e.g. Switch.IsToggled, Picker.SelectedIndex).</summary>
    public async Task SetProperty(string elementId, string name, string value)
    {
        await this.Send($"ui set-property {elementId} {name} {Quote(value)}");
        await Task.Delay(250);
    }

    /// <summary>
    /// Shell.GoToAsync straight from the agent - for resetting state, not for the navigation under
    /// test. The agent waits for Shell to finish, so a guard that puts up a dialog blocks it (and
    /// with it the whole batch session) until the agent gives up.
    /// </summary>
    public Task Navigate(string route, TimeSpan? timeout = null) => this.Send($"ui navigate {route}", timeout);

    /// <summary>Where the app is and what it shows - for failure messages.</summary>
    public async Task<string> Describe()
    {
        try
        {
            var route = await this.Route();
            var labels = (await this.Query(type: "Label")).Where(l => l.IsVisible).Select(l => l.Text).OfType<string>().TakeLast(15);
            var buttons = (await this.Query(type: "Button")).Where(b => b.IsVisible).Select(b => b.Text).OfType<string>().TakeLast(15);
            return $"  route: {route}\n  labels: {String.Join(" | ", labels)}\n  buttons: {String.Join(" | ", buttons)}";
        }
        catch (Exception ex)
        {
            return $"  (could not read the app: {ex.Message})";
        }
    }

    static string Quote(string value)
    {
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
            throw new ArgumentException("Batch arguments may not contain double quotes, semicolons or newlines.", nameof(value));
        return $"\"{value}\"";
    }
}
