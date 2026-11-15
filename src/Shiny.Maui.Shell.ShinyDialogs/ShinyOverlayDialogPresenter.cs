using Microsoft.Maui.Controls.Shapes;
using Shiny.Infrastructure;
using Shiny.Maui.Controls;
using Shiny.Maui.Controls.Themes;

namespace Shiny;

/// <summary>
/// An <see cref="IDialogPresenter"/> that shows the dialog as a themed card floating over the
/// current page on a dimmed backdrop, rather than pushing it onto Shell's modal stack. The page
/// underneath stays visible (and keeps its lifecycle) behind the scrim.
/// </summary>
/// <remarks>
/// The card and the scrim follow the active Shiny theme (<c>Surface</c> and <c>Scrim</c>), so a
/// dialog looks like the rest of a Shiny.Maui.Controls app and identical on every platform.
///
/// <para>The overlay is attached to the page that is current when the dialog is shown. On a
/// <see cref="ShinyContentPage"/> it goes into the page's own <c>OverlayHost</c>, above everything
/// else; on a plain <see cref="ContentPage"/> the page's content is wrapped in a Grid once, and the
/// overlay is layered on top of it.</para>
///
/// <para>The dialog is dismissed - reported to the caller as cancellation - by a backdrop tap, or by
/// the host page disappearing. That second path matters: an overlay lives inside a page, so a
/// navigation away (an Android back press, a tab switch, a programmatic <c>GoBack</c>) takes the
/// dialog with it, and the awaiting caller has to be released rather than left hanging.</para>
/// </remarks>
public class ShinyOverlayDialogPresenter(ShinyDialogPresenterOptions options)
    : ViewDialogPresenter
{
    static readonly Color FallbackSurfaceLight = Color.FromArgb("#FFFFFF");
    static readonly Color FallbackSurfaceDark = Color.FromArgb("#1C1B1F");

    protected override async Task PresentView(View content, object viewModel, CancellationToken dismiss)
    {
        var page = GetTargetPage();
        var host = GetHost(page);

        var scrim = this.BuildScrim();
        var card = this.BuildCard(content);

        var layer = new Grid { Padding = options.Margin, ZIndex = 10_000 };
        layer.Children.Add(scrim);
        layer.Children.Add(card);

        // The scrim has to bleed past the padding that insets the card from the screen edges.
        scrim.Margin = new Thickness(
            -options.Margin.Left,
            -options.Margin.Top,
            -options.Margin.Right,
            -options.Margin.Bottom
        );

        // Dismissal by the user (backdrop tap) or by the page going away underneath us.
        var dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (options.DismissOnBackdropTap)
                dismissed.TrySetResult();
        };
        scrim.GestureRecognizers.Add(tap);

        void OnPageDisappearing(object? sender, EventArgs args) => dismissed.TrySetResult();
        page.Disappearing += OnPageDisappearing;

        try
        {
            host.Children.Add(layer);
            await this.AnimateIn(scrim, card);

            var dismissRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Fires synchronously when the token is already cancelled - the viewmodel completed
            // during the entry animation, so tear straight back down.
            await using var registration = dismiss.Register(() => dismissRequested.TrySetResult());

            await Task.WhenAny(dismissed.Task, dismissRequested.Task);
            await this.AnimateOut(scrim, card);
        }
        finally
        {
            page.Disappearing -= OnPageDisappearing;
            host.Children.Remove(layer);

            // Hands the content back so the base class can return it to its page.
            card.Content = null;
        }
    }


    BoxView BuildScrim()
    {
        var scrim = new BoxView
        {
            Color = options.BackdropColor ?? Colors.Black,
            Opacity = 0
        };
        if (options.BackdropColor == null)
            scrim.SetDynamicResource(BoxView.ColorProperty, ShinyThemeKeys.Color.Scrim);

        return scrim;
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
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.25f,
                Radius = 20,
                Offset = new Point(0, 8)
            },
            Opacity = 0,
            Scale = 0.92
        };

        if (options.CardBackgroundColor == null)
        {
            // The app-theme colours are the fallback: an unresolved dynamic resource - an app that
            // never installed a Shiny theme - leaves whatever was set here.
            card.SetAppThemeColor(Border.BackgroundColorProperty, FallbackSurfaceLight, FallbackSurfaceDark);
            card.SetDynamicResource(Border.BackgroundColorProperty, ShinyThemeKeys.Color.Surface);
        }
        else
        {
            card.BackgroundColor = options.CardBackgroundColor;
        }

        options.ConfigureCard?.Invoke(card);
        return card;
    }


    async Task AnimateIn(BoxView scrim, Border card)
    {
        try
        {
            await Task.WhenAll(
                scrim.FadeToAsync(options.BackdropOpacity, options.AnimationDuration, Easing.CubicOut),
                card.FadeToAsync(1, options.AnimationDuration, Easing.CubicOut),
                card.ScaleToAsync(1, options.AnimationDuration, Easing.SpringOut)
            );
        }
        catch
        {
            // Detached mid-animation (the page went away underneath). Snap to the end state - the
            // dismissal paths below still complete the presentation.
        }
        scrim.Opacity = options.BackdropOpacity;
        card.Opacity = 1;
        card.Scale = 1;
    }


    async Task AnimateOut(BoxView scrim, Border card)
    {
        try
        {
            await Task.WhenAll(
                scrim.FadeToAsync(0, options.AnimationDuration, Easing.CubicIn),
                card.FadeToAsync(0, options.AnimationDuration, Easing.CubicIn),
                card.ScaleToAsync(0.92, options.AnimationDuration, Easing.CubicIn)
            );
        }
        catch
        {
            // See AnimateIn - the layer is removed either way.
        }
    }


    /// <summary>
    /// The layout the overlay is added to. A ShinyContentPage already owns an overlay layer above
    /// its content, so use it; anything else gets its content wrapped once in a Grid.
    /// </summary>
    static Layout GetHost(ContentPage page)
    {
        if (page is ShinyContentPage shinyPage)
            return shinyPage.OverlayHost;

        if (page.Content is DialogHostGrid existing)
            return existing;

        var host = new DialogHostGrid();
        if (page.Content is { } content)
        {
            // Re-parenting requires the page to let go of it first.
            page.Content = null;
            host.Children.Add(content);
        }
        page.Content = host;

        return host;
    }


    /// <summary>Marker so an already-wrapped page is not wrapped again by the next dialog.</summary>
    sealed class DialogHostGrid : Grid;


    static ContentPage GetTargetPage()
    {
        var shell = Shell.Current
            ?? throw new InvalidOperationException("There is no active Shell to present a dialog on");

        // A modal page sits above the Shell, so that is what the user can actually see.
        var modal = shell.Window?.Navigation.ModalStack;
        var page = (modal is { Count: > 0 } ? modal[modal.Count - 1] : shell.CurrentPage)
            ?? throw new InvalidOperationException("The active Shell has no current page to present a dialog on");

        return GetLeafPage(page);
    }


    static ContentPage GetLeafPage(Page page) => page switch
    {
        ContentPage contentPage => contentPage,
        NavigationPage { CurrentPage: { } current } => GetLeafPage(current),
        TabbedPage { CurrentPage: { } current } => GetLeafPage(current),
        FlyoutPage { Detail: { } detail } => GetLeafPage(detail),
        Shell { CurrentPage: { } current } => GetLeafPage(current),
        _ => throw new InvalidOperationException(
            $"Cannot find a ContentPage to host the dialog overlay on - the current page is a '{page.GetType().Name}'"
        )
    };
}
