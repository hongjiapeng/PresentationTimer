using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PresentationTimer.App;

/// <summary>
/// Applies a theme-aware thin acrylic surface to the floating timer window.
/// </summary>
internal sealed class FloatingTimerBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private ElementTheme _theme;

    internal FloatingTimerBackdrop(ElementTheme theme) => this.Theme = theme;

    internal ElementTheme Theme
    {
        get => this._theme;
        set
        {
            this._theme = value;
            this.UpdateTint();
        }
    }

    /// <inheritdoc />
    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        var controller = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin,
        };

        // A presentation HUD normally has no keyboard focus while PowerPoint is active.
        // Keep its material active, while still forwarding accessibility/theme policy.
        var defaults = this.GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
        controller.SetSystemBackdropConfiguration(new SystemBackdropConfiguration
        {
            IsInputActive = true,
            IsHighContrast = defaults.IsHighContrast,
            Theme = defaults.Theme,
        });
        this._controller = controller;
        controller.AddSystemBackdropTarget(connectedTarget);
        this.UpdateTint();
    }

    /// <inheritdoc />
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        this._controller?.RemoveSystemBackdropTarget(disconnectedTarget);
        this._controller?.Dispose();
        this._controller = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }

    private void UpdateTint()
    {
        if (this._controller is not { } controller)
        {
            return;
        }

        string themeKey = this.Theme == ElementTheme.Dark ? "Dark" : "Light";
        var palette = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries[themeKey];
        var tint = (Color)palette["PresenterHudBackdropTintColor"];
        controller.TintColor = tint;
        controller.TintOpacity = (float)(double)palette["PresenterHudBackdropTintOpacity"];
        controller.LuminosityOpacity = (float)(double)palette["PresenterHudBackdropLuminosityOpacity"];
        controller.FallbackColor = tint;
    }
}
