using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PresentationTimer.App.Localization;
using Windows.Graphics;

namespace PresentationTimer.App;

/// <summary>
/// Hosts the application's presenter workspace and owns top-level window behavior.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int CompactHeight = 64;
    private const int CompactWidth = 220;
    private const int PresentationHudHeight = 64;
    private const int PresentationHudWidth = 220;
    private const int PresentationHudEdgeMargin = 16;
    private const int ExpandedHeight = 680;
    private const int ExpandedWidth = 920;
    private const int MinimumExpandedHeight = 600;
    private const int MinimumExpandedWidth = 800;
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowAttributeBorderColor = 34;
    private const int DwmWindowBorderColorDefault = unchecked((int)0xFFFFFFFF);
    private const int DwmWindowBorderColorNone = unchecked((int)0xFFFFFFFE);
    private const int DwmWindowCornerPreferenceDefault = 0;
    private const int DwmWindowCornerPreferenceDoNotRound = 1;
    private const int WindowCornerRadius = 16;
    private const int ResizeAnimationDurationMs = 180;
    private const int ResizeAnimationFrameIntervalMs = 15;
    private const int GetWindowLongStyleIndex = -16;
    private const int SetWindowPositionNoSize = 0x0001;
    private const int SetWindowPositionNoMove = 0x0002;
    private const int SetWindowPositionNoZOrder = 0x0004;
    private const int SetWindowPositionNoActivate = 0x0010;
    private const int SetWindowPositionFrameChanged = 0x0020;
    private const int WindowStyleCaption = 0x00C00000;
    private const int WindowStyleThickFrame = 0x00040000;
    private const uint WindowDisplayAffinityNone = 0;
    private const uint WindowDisplayAffinityExcludeFromCapture = 0x00000011;
    private readonly ILogger<MainWindow> _logger;
    private readonly MainPage _mainPage;
    private readonly WindowController _windowController;
    private readonly DispatcherQueueTimer _resizeAnimationTimer;
    private readonly Stopwatch _animationStopwatch = new Stopwatch();
    private RectInt32? _compactBounds;
    private RectInt32? _expandedBounds;
    private RectInt32? _presentationHudBounds;
    private RectInt32 _animationFrom;
    private RectInt32 _animationTo;
    private int _animationCornerRadiusPx;
    private int _borderColorPreference = DwmWindowBorderColorDefault;
    private DesktopWindowMode _windowMode = DesktopWindowMode.Expanded;
    private bool _shutdownComplete;
    private bool _shutdownStarted;
    private bool _hidePresenterFromCapture = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    internal MainWindow(
        MainPage mainPage,
        LocalizedStrings strings,
        WindowController windowController,
        ILogger<MainWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(mainPage);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(windowController);
        ArgumentNullException.ThrowIfNull(logger);
        this._logger = logger;
        this._mainPage = mainPage;
        this._windowController = windowController;
        this.InitializeComponent();
        this.Title = strings.Get("WindowTitle");
        this.ExtendsContentIntoTitleBar = true;
        this.AppWindow.SetIcon("Assets/AppIcon.ico");
        this.AppWindow.Closing += this.OnClosing;
        this.Activated += this.OnActivated;
        this._mainPage.DragRegionLoaded += this.OnDragRegionLoaded;
        this._mainPage.ActualThemeChanged += this.OnPresenterThemeChanged;
        this.RootFrame.Content = mainPage;
        this._windowController.Attach(this);
        this._resizeAnimationTimer = this.DispatcherQueue.CreateTimer();
        this._resizeAnimationTimer.Interval = TimeSpan.FromMilliseconds(ResizeAnimationFrameIntervalMs);
        this._resizeAnimationTimer.Tick += this.OnResizeAnimationTick;
        this.EnterExpandedMode();
    }

    private enum DesktopWindowMode
    {
        Compact,
        PresentationHud,
        Expanded,
    }

    internal MainPage PresenterPage => this._mainPage;

    internal void EnterCompactMode()
    {
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (this._windowMode == DesktopWindowMode.Expanded)
        {
            this._expandedBounds = this.GetCurrentBounds();
        }
        else if (this._windowMode == DesktopWindowMode.PresentationHud)
        {
            this._presentationHudBounds = this.GetCurrentBounds();
        }

        this.AppTitleBar.Visibility = Visibility.Collapsed;
        this.TitleBarPinButton.Visibility = Visibility.Collapsed;
        this.SetFloatingBackdrop();
        this.WindowLayoutRoot.Background = null;
        presenter.SetBorderAndTitleBar(false, false);
        this.SetWindowChromeVisibility(false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.PreferredMinimumWidth = 0;
        presenter.PreferredMinimumHeight = 0;

        RectInt32 startBounds = this.GetCurrentBounds();
        RectInt32 target = this._compactBounds ?? startBounds;
        target.Width = this.ToPhysicalPixels(CompactWidth);
        target.Height = this.ToPhysicalPixels(CompactHeight);
        target = ClampToVisibleWorkArea(target);
        this._compactBounds = target;
        this._windowMode = DesktopWindowMode.Compact;
        this.SetTitleBarIfLoaded(this._mainPage.ActiveDragRegion);
        this.RequestCornerPreference(DwmWindowCornerPreferenceDoNotRound);
        this.RequestBorderColor(DwmWindowBorderColorNone);

        // Resize immediately when switching out of the presenter surface. This avoids
        // rebuilding an x:Load visual tree while a native window-region animation is
        // still applying DWM regions frame by frame.
        this._resizeAnimationTimer.Stop();
        this.AppWindow.MoveAndResize(target);
        this.ApplyRoundedWindowRegion(target.Width, target.Height, this.ToPhysicalPixels(WindowCornerRadius));
        this.UpdateCaptureAffinity();
    }

    internal void EnterPresentationHudMode()
    {
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (this._windowMode == DesktopWindowMode.Compact)
        {
            this._compactBounds = this.GetCurrentBounds();
        }
        else if (this._windowMode == DesktopWindowMode.Expanded)
        {
            this._expandedBounds = this.GetCurrentBounds();
        }

        this.AppTitleBar.Visibility = Visibility.Collapsed;
        this.TitleBarPinButton.Visibility = Visibility.Collapsed;
        this.SetFloatingBackdrop();
        this.WindowLayoutRoot.Background = null;
        presenter.SetBorderAndTitleBar(false, false);
        this.SetWindowChromeVisibility(false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.PreferredMinimumWidth = 0;
        presenter.PreferredMinimumHeight = 0;

        int width = this.ToPhysicalPixels(PresentationHudWidth);
        int height = this.ToPhysicalPixels(PresentationHudHeight);
        RectInt32 target;
        if (this._presentationHudBounds is RectInt32 savedBounds)
        {
            target = new RectInt32(savedBounds.X, savedBounds.Y, width, height);
        }
        else
        {
            // Dock on the display that currently hosts the presenter controls.
            // A manually moved HUD keeps its saved position on later visits.
            RectInt32 workArea = DisplayArea.GetFromRect(this.GetCurrentBounds(), DisplayAreaFallback.Primary).WorkArea;
            int margin = this.ToPhysicalPixels(PresentationHudEdgeMargin);
            target = new RectInt32(
                workArea.X + workArea.Width - width - margin,
                workArea.Y + workArea.Height - height - margin,
                width,
                height);
        }

        target = ClampToVisibleWorkArea(target);
        this._presentationHudBounds = target;
        this._windowMode = DesktopWindowMode.PresentationHud;
        this.SetTitleBarIfLoaded(this._mainPage.ActiveDragRegion);
        this.RequestCornerPreference(DwmWindowCornerPreferenceDoNotRound);
        this.RequestBorderColor(DwmWindowBorderColorNone);
        this._resizeAnimationTimer.Stop();
        this.AppWindow.MoveAndResize(target);
        this.ApplyRoundedWindowRegion(target.Width, target.Height, this.ToPhysicalPixels(WindowCornerRadius));
        this.UpdateCaptureAffinity();
    }

    internal void EnterExpandedMode()
    {
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        this._resizeAnimationTimer.Stop();
        this.SystemBackdrop = new MicaBackdrop();
        this.WindowLayoutRoot.Background = (Brush)Application.Current.Resources["PresenterWindowSurfaceBrush"];

        if (this._windowMode == DesktopWindowMode.Compact)
        {
            this._compactBounds = this.GetCurrentBounds();
        }
        else if (this._windowMode == DesktopWindowMode.PresentationHud)
        {
            this._presentationHudBounds = this.GetCurrentBounds();
        }

        presenter.SetBorderAndTitleBar(true, true);
        this.SetWindowChromeVisibility(true);
        presenter.IsResizable = true;
        presenter.IsMaximizable = true;
        presenter.IsMinimizable = true;
        presenter.PreferredMinimumWidth = this.ToPhysicalPixels(MinimumExpandedWidth);
        presenter.PreferredMinimumHeight = this.ToPhysicalPixels(MinimumExpandedHeight);
        this.AppTitleBar.Visibility = Visibility.Visible;
        this.TitleBarPinButton.Visibility = Visibility.Visible;
        this.UpdateTitleBarPinMargin();
        this.SetTitleBar(this.AppTitleBar);
        this.RequestCornerPreference(DwmWindowCornerPreferenceDefault);
        this.RequestBorderColor(DwmWindowBorderColorDefault);
        this.ClearWindowRegion();

        RectInt32 target = this._expandedBounds ?? this.GetCurrentBounds();
        if (this._expandedBounds is null)
        {
            target.Width = this.ToPhysicalPixels(ExpandedWidth);
            target.Height = this.ToPhysicalPixels(ExpandedHeight);
        }

        target = ClampToVisibleWorkArea(target);
        this.AppWindow.MoveAndResize(target);
        this._expandedBounds = target;

        this._windowMode = DesktopWindowMode.Expanded;
        this.UpdateCaptureAffinity();
    }

    internal void SetAlwaysOnTop(bool isAlwaysOnTop)
    {
        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isAlwaysOnTop;
        }
    }

    internal void StopUiNotifications() => this._mainPage.PrepareForShutdown();

    internal void CloseForLanguageChange()
    {
        this._resizeAnimationTimer.Stop();
        this._shutdownComplete = true;
        this.AppWindow.Closing -= this.OnClosing;
        this.Activated -= this.OnActivated;
        this._mainPage.DragRegionLoaded -= this.OnDragRegionLoaded;
        this._mainPage.ActualThemeChanged -= this.OnPresenterThemeChanged;
        this._windowController.Detach(this);
        this.Close();
    }

    internal void SetHidePresenterFromCapture(bool hidePresenterFromCapture)
    {
        this._hidePresenterFromCapture = hidePresenterFromCapture;
        this.UpdateCaptureAffinity();
    }

    internal RectInt32 CopyWindowStateTo(MainWindow replacementWindow)
    {
        ArgumentNullException.ThrowIfNull(replacementWindow);

        RectInt32 appWindowBounds = this.GetCurrentBounds();
        RectInt32 nativeWindowBounds = this.GetNativeWindowBounds();
        replacementWindow.PresenterPage.IsAlwaysOnTop = this.PresenterPage.IsAlwaysOnTop;
        replacementWindow.PresenterPage.IsHiddenFromCapture = this.PresenterPage.IsHiddenFromCapture;
        replacementWindow._presentationHudBounds = this._windowMode == DesktopWindowMode.PresentationHud
            ? appWindowBounds
            : this._presentationHudBounds;
        replacementWindow.AppWindow.MoveAndResize(appWindowBounds);

        switch (this._windowMode)
        {
            case DesktopWindowMode.Compact:
                replacementWindow.PresenterPage.RestoreCompactMode();
                replacementWindow.EnterCompactMode();
                break;
            case DesktopWindowMode.PresentationHud:
                replacementWindow.PresenterPage.RestorePresentationHudMode();
                replacementWindow.EnterPresentationHudMode();
                break;
            default:
                replacementWindow._expandedBounds = appWindowBounds;
                break;
        }

        replacementWindow.SetNativeWindowBounds(nativeWindowBounds);
        return nativeWindowBounds;
    }

    internal void RestoreNativeWindowBounds(RectInt32 bounds) => this.SetNativeWindowBounds(bounds);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint windowHandle, uint affinity);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(
        int leftRect,
        int topRect,
        int rightRect,
        int bottomRect,
        int widthEllipse,
        int heightEllipse);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint windowHandle, nint region, bool redraw);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint windowHandle, out NativeWindowBounds bounds);

    [LoggerMessage(4000, LogLevel.Error, "Window shutdown encountered an error")]
    private static partial void LogWindowShutdownFailed(ILogger logger, Exception exception);

    [LoggerMessage(4001, LogLevel.Warning, "Could not update presenter capture visibility (Win32 error {ErrorCode})")]
    private static partial void LogCaptureAffinityFailed(ILogger logger, int errorCode);

    private static RectInt32 ClampToVisibleWorkArea(RectInt32 bounds)
    {
        DisplayArea displayArea = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
        int width = Math.Min(bounds.Width, workArea.Width);
        int height = Math.Min(bounds.Height, workArea.Height);
        int maximumX = workArea.X + workArea.Width - width;
        int maximumY = workArea.Y + workArea.Height - height;

        return new RectInt32(
            Math.Clamp(bounds.X, workArea.X, maximumX),
            Math.Clamp(bounds.Y, workArea.Y, maximumY),
            width,
            height);
    }

    private static int Lerp(int from, int to, double t) => (int)Math.Round(from + ((to - from) * t));

    private static void RefreshWindowFrame(nint windowHandle)
    {
        int flags = SetWindowPositionNoSize |
            SetWindowPositionNoMove |
            SetWindowPositionNoZOrder |
            SetWindowPositionNoActivate |
            SetWindowPositionFrameChanged;
        _ = SetWindowPos(windowHandle, 0, 0, 0, 0, 0, flags);
    }

    private void UpdateCaptureAffinity()
    {
        uint affinity = this._hidePresenterFromCapture && this._windowMode != DesktopWindowMode.Expanded
            ? WindowDisplayAffinityExcludeFromCapture
            : WindowDisplayAffinityNone;
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        if (!SetWindowDisplayAffinity(windowHandle, affinity))
        {
            LogCaptureAffinityFailed(this._logger, Marshal.GetLastWin32Error());
        }
    }

    private RectInt32 GetCurrentBounds() => new RectInt32(
        this.AppWindow.Position.X,
        this.AppWindow.Position.Y,
        this.AppWindow.Size.Width,
        this.AppWindow.Size.Height);

    private RectInt32 GetNativeWindowBounds()
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        if (!GetWindowRect(windowHandle, out NativeWindowBounds bounds))
        {
            return this.GetCurrentBounds();
        }

        return new RectInt32(
            bounds.Left,
            bounds.Top,
            bounds.Right - bounds.Left,
            bounds.Bottom - bounds.Top);
    }

    private void SetNativeWindowBounds(RectInt32 bounds)
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        _ = SetWindowPos(
            windowHandle,
            0,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SetWindowPositionNoZOrder | SetWindowPositionNoActivate);
    }

    private void RequestCornerPreference(int preference)
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowAttributeCornerPreference,
            ref preference,
            sizeof(int));
    }

    private void RequestBorderColor(int color)
    {
        this._borderColorPreference = color;
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowAttributeBorderColor,
            ref color,
            sizeof(int));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args) =>
        this.RequestBorderColor(this._borderColorPreference);

    private void OnPresenterThemeChanged(FrameworkElement sender, object args)
    {
        if (this.SystemBackdrop is FloatingTimerBackdrop backdrop)
        {
            backdrop.Theme = sender.ActualTheme;
        }
    }

    private void SetFloatingBackdrop() =>
        this.SystemBackdrop = new FloatingTimerBackdrop(this._mainPage.ActualTheme);

    private void WindowLayoutRoot_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (this._windowMode == DesktopWindowMode.Expanded)
        {
            this.UpdateTitleBarPinMargin();
        }
    }

    private void SetWindowChromeVisibility(bool isVisible)
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        nint windowStyle = GetWindowLongPtr(windowHandle, GetWindowLongStyleIndex);
        nint chromeStyle = WindowStyleCaption | WindowStyleThickFrame;
        nint updatedStyle = isVisible
            ? windowStyle | chromeStyle
            : windowStyle & ~chromeStyle;
        _ = SetWindowLongPtr(windowHandle, GetWindowLongStyleIndex, updatedStyle);
        RefreshWindowFrame(windowHandle);
    }

    private void SetTitleBarIfLoaded(FrameworkElement? dragRegion)
    {
        if (dragRegion?.XamlRoot is not null)
        {
            this.SetTitleBar(dragRegion);
        }
    }

    private void ApplyRoundedWindowRegion(int widthPx, int heightPx, int radiusPx)
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);

        // The native frame can paint a one-pixel dark seam above the XAML surface.
        // Keep that row outside the existing rounded window region.
        nint region = CreateRoundRectRgn(0, 1, widthPx, heightPx, radiusPx * 2, radiusPx * 2);
        _ = SetWindowRgn(windowHandle, region, true);
    }

    private void ClearWindowRegion()
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        _ = SetWindowRgn(windowHandle, 0, true);
    }

    private void BeginResizeAnimation(RectInt32 from, RectInt32 to, int cornerRadiusPx)
    {
        this._resizeAnimationTimer.Stop();

        bool unchanged = from.X == to.X && from.Y == to.Y && from.Width == to.Width && from.Height == to.Height;
        if (unchanged)
        {
            this.AppWindow.MoveAndResize(to);
            this.ApplyRoundedWindowRegion(to.Width, to.Height, cornerRadiusPx);
            return;
        }

        this._animationFrom = from;
        this._animationTo = to;
        this._animationCornerRadiusPx = cornerRadiusPx;
        this._animationStopwatch.Restart();
        this._resizeAnimationTimer.Start();
    }

    private void OnResizeAnimationTick(DispatcherQueueTimer sender, object args)
    {
        double t = Math.Min(1d, this._animationStopwatch.Elapsed.TotalMilliseconds / ResizeAnimationDurationMs);
        double eased = 1d - Math.Pow(1d - t, 3d);
        RectInt32 step = new RectInt32(
            Lerp(this._animationFrom.X, this._animationTo.X, eased),
            Lerp(this._animationFrom.Y, this._animationTo.Y, eased),
            Lerp(this._animationFrom.Width, this._animationTo.Width, eased),
            Lerp(this._animationFrom.Height, this._animationTo.Height, eased));

        this.AppWindow.MoveAndResize(step);
        this.ApplyRoundedWindowRegion(step.Width, step.Height, this._animationCornerRadiusPx);

        if (t >= 1d)
        {
            this._resizeAnimationTimer.Stop();
        }
    }

    private void OnDragRegionLoaded(FrameworkElement dragRegion) => this.SetTitleBar(dragRegion);

    private int ToPhysicalPixels(int effectivePixels)
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        uint dpi = GetDpiForWindow(windowHandle);
        double scale = dpi == 0 ? 1d : dpi / 96d;
        return checked((int)Math.Round(effectivePixels * scale));
    }

    private double ToEffectivePixels(int physicalPixels)
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        uint dpi = GetDpiForWindow(windowHandle);
        double scale = dpi == 0 ? 1d : dpi / 96d;
        return physicalPixels / scale;
    }

    private void UpdateTitleBarPinMargin()
    {
        double rightInset = this.ToEffectivePixels(this.AppWindow.TitleBar.RightInset);
        this.TitleBarPinButton.Margin = new Thickness(0, 0, rightInset + 8, 4);
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (this._shutdownComplete)
        {
            this.AppWindow.Closing -= this.OnClosing;
            return;
        }

        args.Cancel = true;
        if (this._shutdownStarted)
        {
            return;
        }

        this._shutdownStarted = true;
        try
        {
            await ((App)Application.Current).ShutdownAsync();
        }
        catch (Exception exception)
        {
            LogWindowShutdownFailed(this._logger, exception);
        }
        finally
        {
            this.Activated -= this.OnActivated;
            this._mainPage.DragRegionLoaded -= this.OnDragRegionLoaded;
            this._mainPage.ActualThemeChanged -= this.OnPresenterThemeChanged;
            this._windowController.Detach(this);
            this._shutdownComplete = true;
            this.Close();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowBounds
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }
}
