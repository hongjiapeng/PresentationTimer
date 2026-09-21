using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PresentationTimer.App;

/// <summary>
/// Applies theme-aware desktop acrylic to the compact presenter window.
/// </summary>
internal sealed class FloatingTimerBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;

    /// <inheritdoc />
    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        if (this._controller is not null)
        {
            throw new InvalidOperationException("A floating timer backdrop cannot be shared between windows.");
        }

        var controller = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin,
        };
        SystemBackdropConfiguration configuration = this.GetDefaultSystemBackdropConfiguration(
            connectedTarget,
            xamlRoot);
        controller.SetSystemBackdropConfiguration(configuration);
        controller.AddSystemBackdropTarget(connectedTarget);
        this._controller = controller;
        this.ApplyPalette(configuration);
    }

    /// <inheritdoc />
    protected override void OnDefaultSystemBackdropConfigurationChanged(
        ICompositionSupportsSystemBackdrop target,
        XamlRoot xamlRoot)
    {
        if (this._controller is null)
        {
            return;
        }

        SystemBackdropConfiguration configuration = this.GetDefaultSystemBackdropConfiguration(target, xamlRoot);
        this.ApplyPalette(configuration);
    }

    /// <inheritdoc />
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        if (this._controller is { } controller)
        {
            controller.RemoveSystemBackdropTarget(disconnectedTarget);
            controller.Dispose();
            this._controller = null;
        }
    }

    private void ApplyPalette(SystemBackdropConfiguration configuration)
    {
        if (this._controller is not { } controller)
        {
            return;
        }

        if (configuration.IsHighContrast)
        {
            controller.ResetProperties();
            return;
        }

        string themeKey = configuration.Theme == SystemBackdropTheme.Dark ? "Dark" : "Light";
        var palette = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries[themeKey];
        var tint = (Color)palette["PresenterHudBackdropTintColor"];
        controller.TintColor = tint;
        controller.TintOpacity = (float)(double)palette["PresenterHudBackdropTintOpacity"];
        controller.LuminosityOpacity = (float)(double)palette["PresenterHudBackdropLuminosityOpacity"];
        controller.FallbackColor = tint;
    }
}
