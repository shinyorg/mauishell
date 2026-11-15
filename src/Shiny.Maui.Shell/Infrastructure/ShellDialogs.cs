namespace Shiny.Infrastructure;

public class ShellDialogs : IDialogs
{
    public Task Alert(string? title, string message, string acceptText = "OK")
        => MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.DisplayAlertAsync(title, message, acceptText)
        );
    

    public Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No")
        => MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.DisplayAlertAsync(title, message, acceptText, cancelText)
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
    ) => MainThread.InvokeOnMainThreadAsync(() =>
        Shell.Current.DisplayPromptAsync(
            title ?? String.Empty,
            message,
            acceptText,
            cancelText,
            placeholder,
            maxLength,
            keyboard,
            initialValue
        )
    );


    public Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons)
        => MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.DisplayActionSheetAsync(title, cancel, destruction, buttons)
        );
}