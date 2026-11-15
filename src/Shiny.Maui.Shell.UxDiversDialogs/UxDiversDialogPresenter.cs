using Microsoft.Maui.Controls.Shapes;
using Shiny.Infrastructure;
using UXDivers.Popups;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace Shiny;

/// <summary>
/// An <see cref="IDialogPresenter"/> that shows the dialog in a UXDivers <see cref="PopupPage"/> -
/// a card over a dimmed backdrop - rather than pushing it onto Shell's modal stack. The page
/// underneath stays visible (and keeps its lifecycle) behind the scrim.
/// </summary>
/// <remarks>
/// The popup is built the way UXDivers' own custom popups are: a <see cref="Border"/> card filled
/// with the theme's <c>PopupBorderColor</c>, over a backdrop filled with <c>PopupBackdropColor</c>,
/// so a ViewModel dialog matches the alert/confirm/prompt popups from
/// <see cref="UxDiversDialogs"/>.
///
/// <para>Dismissal - reported to the caller as cancellation - is a backdrop tap or, on Android, the
/// hardware back button, which UXDivers Popups maps to closing the topmost popup unless
/// <c>UseUXDiversPopups(closePopupOnBackAndroid: false)</c> says otherwise.</para>
/// </remarks>
public class UxDiversDialogPresenter(UxDiversDialogPresenterOptions options)
    : ViewDialogPresenter
{
    const string BackdropColorKey = "PopupBackdropColor";
    const string CardColorKey = "PopupBorderColor";

    static readonly Color FallbackCardLight = Color.FromArgb("#FFFFFF");
    static readonly Color FallbackCardDark = Color.FromArgb("#1F1F1F");

    protected override async Task PresentView(View content, object viewModel, CancellationToken dismiss)
    {
        var card = this.BuildCard(content);
        var popup = this.BuildPopup(card);

        // Raised for every close - the action that popped it, a backdrop tap, or the Android back
        // button - so it is the one signal that covers user dismissal.
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object? sender, PopupEventArgs args) => closed.TrySetResult();
        popup.PopupClosed += OnClosed;

        try
        {
            // PushAsync defaults to waitUntilClosed, so this task completes when the popup is gone.
            var presentation = IPopupService.Current.PushAsync(popup);

            var dismissRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Fires synchronously when the token is already cancelled - the viewmodel completed
            // before the push settled, so tear straight back down.
            await using var registration = dismiss.Register(() => dismissRequested.TrySetResult());

            await Task.WhenAny(presentation, closed.Task, dismissRequested.Task);

            // A completed push just means the popup closed; a faulted one is a real failure and
            // awaiting it here is what surfaces it.
            if (presentation.IsCompleted)
                await presentation;
        }
        finally
        {
            popup.PopupClosed -= OnClosed;
            await this.Pop(popup);

            // Hands the content back so the base class can return it to its page.
            popup.PopupContent = null;
            card.Content = null;
        }
    }


    async Task Pop(PopupPage popup)
    {
        try
        {
            if (IPopupService.Current.NavigationStack.Any(x => ReferenceEquals(x, popup)))
                await IPopupService.Current.PopAsync(popup);
        }
        catch
        {
            // Already gone - the user dismissed it, or the stack was cleared underneath us.
        }
    }


    Border BuildCard(View content)
    {
        var card = new Border
        {
            Content = content,
            Padding = 0,
            StrokeThickness = 0,
            Stroke = Brush.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = options.CornerRadius },
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = options.MaxWidth,
            Margin = options.Margin
        };

        if (options.CardBackgroundColor == null)
        {
            // The app-theme colours are the fallback: an unresolved dynamic resource - an app that
            // never merged the UXDivers theme dictionaries - leaves whatever was set here.
            card.SetAppThemeColor(Border.BackgroundColorProperty, FallbackCardLight, FallbackCardDark);
            card.SetDynamicResource(Border.BackgroundColorProperty, CardColorKey);
        }
        else
        {
            card.BackgroundColor = options.CardBackgroundColor;
        }

        return card;
    }


    PopupPage BuildPopup(View card)
    {
        var popup = new PopupPage
        {
            PopupContent = card,
            BackgroundOpacity = options.BackdropOpacity,
            CloseWhenBackgroundIsClicked = options.DismissOnBackdropTap,
            AvoidKeyboard = options.AvoidKeyboard,
            // The stock AppearingPopupAnimation is a fixed-duration storyboard, so the fade and
            // scale are composed here instead - same shape, with the duration under our control.
            AppearingAnimation = new StoryboardAnimation
            {
                Strategy = StoryboardStrategy.RunAllAtStart,
                Animation1 = new FadeInPopupAnimation { Duration = options.AnimationDuration },
                Animation2 = new ScaleInPopupAnimation { Duration = options.AnimationDuration, ScaleFrom = 0.92 }
            },
            DisappearingAnimation = new StoryboardAnimation
            {
                Strategy = StoryboardStrategy.RunAllAtStart,
                Animation1 = new FadeOutPopupAnimation { Duration = options.AnimationDuration },
                Animation2 = new ScaleOutPopupAnimation { Duration = options.AnimationDuration, ScaleTo = 0.92 }
            }
        };

        if (options.BackdropColor == null)
        {
            popup.BackgroundColor = Colors.Black;
            popup.SetDynamicResource(VisualElement.BackgroundColorProperty, BackdropColorKey);
        }
        else
        {
            popup.BackgroundColor = options.BackdropColor;
        }

        options.ConfigurePopup?.Invoke(popup);
        return popup;
    }
}
