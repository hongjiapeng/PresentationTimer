using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PresentationTimer.App;

/// <summary>
/// Applies a lightly tinted thin acrylic surface to the floating timer window.
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

        var controller = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin,
            TintColor = Color.FromArgb(255, 32, 40, 51),
            TintOpacity = 0.14f,
            LuminosityOpacity = 0.30f,
        };
        controller.SetSystemBackdropConfiguration(
            this.GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot));
        controller.AddSystemBackdropTarget(connectedTarget);
        this._controller = controller;
    }

    /// <inheritdoc />
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        this._controller?.RemoveSystemBackdropTarget(disconnectedTarget);
        this._controller?.Dispose();
        this._controller = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }
}
