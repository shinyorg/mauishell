using UXDivers.Popups;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Shiny.Infrastructure;

namespace Shiny;

public class UxDiversDialogs : IDialogs
{
    // NOTE: PopupServiceCore raises PopupClosed *inside* PopAsync (before its task completes).
    // Each action command must therefore unsubscribe OnPopupClosed before popping and resolve
    // the result itself; OnPopupClosed only handles dismissal without a selection
    // (tap-outside / back button), where it falls back to the cancel/default value.

    public Task Alert(string? title, string message, string acceptText = "OK")
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var tcs = new TaskCompletionSource();
            SimpleActionPopup popup = null!;
            void OnPopupClosed(object? sender, PopupEventArgs e)
            {
                popup.PopupClosed -= OnPopupClosed;
                tcs.TrySetResult();
            }
            popup = new SimpleActionPopup
            {
                Title = title ?? string.Empty,
                Text = message,
                ActionButtonText = acceptText,
                ActionButtonCommand = new Command(async () =>
                {
                    popup.PopupClosed -= OnPopupClosed;
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult();
                }),
                ShowSecondaryActionButton = false
            };
            popup.PopupClosed += OnPopupClosed;
            await IPopupService.Current.PushAsync(popup);
            await tcs.Task;
        });


    public Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No")
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var tcs = new TaskCompletionSource<bool>();
            SimpleActionPopup popup = null!;
            void OnPopupClosed(object? sender, PopupEventArgs e)
            {
                popup.PopupClosed -= OnPopupClosed;
                tcs.TrySetResult(false);
            }
            popup = new SimpleActionPopup
            {
                Title = title ?? string.Empty,
                Text = message,
                ActionButtonText = acceptText,
                ActionButtonCommand = new Command(async () =>
                {
                    popup.PopupClosed -= OnPopupClosed;
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult(true);
                }),
                SecondaryActionButtonText = cancelText,
                SecondaryActionButtonCommand = new Command(async () =>
                {
                    popup.PopupClosed -= OnPopupClosed;
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult(false);
                })
            };
            popup.PopupClosed += OnPopupClosed;
            await IPopupService.Current.PushAsync(popup);
            return await tcs.Task;
        });


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
        var field = new FormField
        {
            Placeholder = placeholder,
            Value = initialValue
        };
        var tcs = new TaskCompletionSource<string?>();
        FormPopup popup = null!;
        void OnPopupClosed(object? sender, PopupEventArgs e)
        {
            popup.PopupClosed -= OnPopupClosed;
            tcs.TrySetResult(null);
        }
        popup = new FormPopup
        {
            Title = title ?? string.Empty,
            Text = message,
            Items = new[] { field },
            ActionButtonText = acceptText,
            ActionButtonCommand = new Command(async () =>
            {
                popup.PopupClosed -= OnPopupClosed;
                var result = field.Value;
                await IPopupService.Current.PopAsync();
                tcs.TrySetResult(result);
            }),
            SecondaryActionLinkText = cancelText,
            SecondaryActionLinkCommand = new Command(async () =>
            {
                popup.PopupClosed -= OnPopupClosed;
                await IPopupService.Current.PopAsync();
                tcs.TrySetResult(null);
            })
        };
        popup.PopupClosed += OnPopupClosed;
        await IPopupService.Current.PushAsync(popup);
        return await tcs.Task;
    });


    public Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var tcs = new TaskCompletionSource<string>();
            var items = new List<OptionSheetItem>();
            OptionSheetPopup popup = null!;

            void OnPopupClosed(object? sender, PopupEventArgs e)
            {
                popup.PopupClosed -= OnPopupClosed;
                tcs.TrySetResult(cancel ?? string.Empty);
            }

            foreach (var button in buttons)
            {
                items.Add(new OptionSheetItem
                {
                    Text = button,
                    Command = new Command(async () =>
                    {
                        popup.PopupClosed -= OnPopupClosed;
                        await IPopupService.Current.PopAsync();
                        tcs.TrySetResult(button);
                    })
                });
            }

            if (destruction != null)
            {
                items.Add(new OptionSheetItem
                {
                    Text = destruction,
                    IconColor = Colors.Red,
                    Command = new Command(async () =>
                    {
                        popup.PopupClosed -= OnPopupClosed;
                        await IPopupService.Current.PopAsync();
                        tcs.TrySetResult(destruction);
                    })
                });
            }

            popup = new OptionSheetPopup
            {
                Title = title ?? string.Empty,
                Items = items
            };

            if (cancel != null)
            {
                popup.CloseButtonCommand = new Command(async () =>
                {
                    popup.PopupClosed -= OnPopupClosed;
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult(cancel);
                });
            }

            popup.PopupClosed += OnPopupClosed;
            await IPopupService.Current.PushAsync(popup);
            return await tcs.Task;
        });
}
