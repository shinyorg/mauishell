using Shiny.Infrastructure;
using Shiny.Maui.Controls.Dialogs;

namespace Shiny;

/// <summary>
/// An <see cref="IDialogs"/> implementation backed by the owned, animated, themeable
/// dialog service from Shiny.Maui.Controls (<see cref="IDialogService"/>). Unlike the
/// default <c>ShellDialogs</c> (which uses the native platform alert/prompt), these dialogs
/// are rendered by the library so they look identical across platforms.
/// </summary>
public class ShinyDialogs(IDialogService dialogs) : IDialogs
{
    public Task Alert(string? title, string message, string acceptText = "OK")
        => MainThread.InvokeOnMainThreadAsync(() =>
            dialogs.Alert(title ?? String.Empty, message, acceptText)
        );


    public Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No")
        => MainThread.InvokeOnMainThreadAsync(() =>
            dialogs.Confirm(title ?? String.Empty, message, acceptText, cancelText)
        );


    public Task<string?> Prompt(
        string? title,
        string message,
        string acceptText = "OK",
        string cancelText = "Cancel",
        string? placeholder = null,
        string initialValue = "",
        int maxLength = -1,
        Keyboard? keyboard = null
    ) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        var result = await dialogs.Prompt(
            title ?? String.Empty,
            message,
            placeholder,
            acceptText,
            cancelText,
            initialValue,
            maxLength > 0 ? maxLength : null,
            keyboard
        );
        return result.Ok ? result.Value : null;
    });


    public Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var result = await dialogs.ActionSheet(
                title ?? String.Empty,
                buttons,
                cancel,
                destruction
            );
            return result ?? cancel ?? String.Empty;
        });
}
