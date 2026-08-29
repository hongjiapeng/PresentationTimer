using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PresentationTimer.App.Localization;
using Windows.Graphics;

namespace PresentationTimer.App;

/// <summary>
/// Hosts the application's presenter workspace and owns top-level window behavior.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int CompactHeight = 240;
    private const int CompactWidth = 440;
    private const int PresentationHudHeight = 96;
    private const int PresentationHudWidth = 288;
    private const int PresentationHudWorkAreaInset = 12;
    private const int ExpandedHeight = 680;
    private const int ExpandedWidth = 920;
    private const int MinimumExpandedHeight = 600;
    private const int MinimumExpandedWidth = 800;
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowAttributeBorderColor = 34;
    private const int DwmWindowBorderColorDefault = unchecked((int)0xFFFFFFFF);
    private const int DwmWindowBorderColorNone = unchecked((int)0xFFFFFFFE);
    private const int DwmWindowCornerPreferenceDefault = 0;
    private const int DwmWindowCornerPreferenceRoundSmall = 3;
    private const int WindowCornerRadius = 12;
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
    private nint _originalWindowStyle;
    private bool _hasOriginalWindowStyle;
    private DesktopWindowMode _windowMode = DesktopWindowMode.Compact;
    private bool _shutdownComplete;
    private bool _shutdownStarted;

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
        this.RootFrame.Content = mainPage;
        this._windowController.Attach(this);
        this._resizeAnimationTimer = this.DispatcherQueue.CreateTimer();
        this._resizeAnimationTimer.Interval = TimeSpan.FromMilliseconds(ResizeAnimationFrameIntervalMs);
        this._resizeAnimationTimer.Tick += this.OnResizeAnimationTick;
        this.EnterCompactMode();
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

        this.CaptureOriginalWindowStyle();

        if (this._windowMode == DesktopWindowMode.Expanded)
        {
            this._expandedBounds = this.GetCurrentBounds();
        }
        else if (this._windowMode == DesktopWindowMode.PresentationHud)
        {
            this._presentationHudBounds = this.GetCurrentBounds();
        }

        this.AppTitleBar.Visibility = Visibility.Collapsed;
        presenter.SetBorderAndTitleBar(false, false);
        this.RemoveWindowChrome();
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
        this.RequestCornerPreference(DwmWindowCornerPreferenceRoundSmall);
        this.RequestBorderColor(DwmWindowBorderColorNone);
        this.BeginResizeAnimation(startBounds, target, this.ToPhysicalPixels(WindowCornerRadius));
    }

    internal void EnterPresentationHudMode()
    {
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        this.CaptureOriginalWindowStyle();

        if (this._windowMode == DesktopWindowMode.Compact)
        {
            this._compactBounds = this.GetCurrentBounds();
        }
        else if (this._windowMode == DesktopWindowMode.Expanded)
        {
            this._expandedBounds = this.GetCurrentBounds();
        }

        this.AppTitleBar.Visibility = Visibility.Collapsed;
        presenter.SetBorderAndTitleBar(false, false);
        this.RemoveWindowChrome();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.PreferredMinimumWidth = 0;
        presenter.PreferredMinimumHeight = 0;

        bool hasRetainedBounds = this._presentationHudBounds is not null;
        RectInt32 startBounds = this.GetCurrentBounds();
        RectInt32 target = this._presentationHudBounds ?? startBounds;
        target.Width = this.ToPhysicalPixels(PresentationHudWidth);
        target.Height = this.ToPhysicalPixels(PresentationHudHeight);
        target = hasRetainedBounds
            ? ClampToVisibleWorkArea(target)
            : SnapToNearestWorkAreaCorner(target, this.ToPhysicalPixels(PresentationHudWorkAreaInset));
        this._presentationHudBounds = target;
        this._windowMode = DesktopWindowMode.PresentationHud;
        this.SetTitleBarIfLoaded(this._mainPage.ActiveDragRegion);
        this.RequestCornerPreference(DwmWindowCornerPreferenceRoundSmall);
        this.RequestBorderColor(DwmWindowBorderColorNone);
        this.BeginResizeAnimation(startBounds, target, this.ToPhysicalPixels(WindowCornerRadius));
    }

    internal void EnterExpandedMode()
    {
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        this._resizeAnimationTimer.Stop();

        if (this._windowMode == DesktopWindowMode.Compact)
        {
            this._compactBounds = this.GetCurrentBounds();
        }
        else if (this._windowMode == DesktopWindowMode.PresentationHud)
        {
            this._presentationHudBounds = this.GetCurrentBounds();
        }

        presenter.SetBorderAndTitleBar(true, true);
        this.RestoreOriginalWindowStyle();
        presenter.IsResizable = true;
        presenter.IsMaximizable = true;
        presenter.IsMinimizable = true;
        presenter.PreferredMinimumWidth = this.ToPhysicalPixels(MinimumExpandedWidth);
        presenter.PreferredMinimumHeight = this.ToPhysicalPixels(MinimumExpandedHeight);
        this.AppTitleBar.Visibility = Visibility.Visible;
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
    }

    internal void SetAlwaysOnTop(bool isAlwaysOnTop)
    {
        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isAlwaysOnTop;
        }
    }

    internal void StopUiNotifications() => this._mainPage.PrepareForShutdown();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

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

    [LoggerMessage(4000, LogLevel.Error, "Window shutdown encountered an error")]
    private static partial void LogWindowShutdownFailed(ILogger logger, Exception exception);

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

    private static RectInt32 SnapToNearestWorkAreaCorner(RectInt32 bounds, int inset)
    {
        DisplayArea displayArea = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
        int width = Math.Min(bounds.Width, Math.Max(1, workArea.Width - (inset * 2)));
        int height = Math.Min(bounds.Height, Math.Max(1, workArea.Height - (inset * 2)));
        int boundsCenterX = bounds.X + (bounds.Width / 2);
        int boundsCenterY = bounds.Y + (bounds.Height / 2);
        int workAreaCenterX = workArea.X + (workArea.Width / 2);
        int workAreaCenterY = workArea.Y + (workArea.Height / 2);
        int x = boundsCenterX < workAreaCenterX
            ? workArea.X + inset
            : workArea.X + workArea.Width - width - inset;
        int y = boundsCenterY < workAreaCenterY
            ? workArea.Y + inset
            : workArea.Y + workArea.Height - height - inset;

        return ClampToVisibleWorkArea(new RectInt32(x, y, width, height));
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

    private RectInt32 GetCurrentBounds() => new RectInt32(
        this.AppWindow.Position.X,
        this.AppWindow.Position.Y,
        this.AppWindow.Size.Width,
        this.AppWindow.Size.Height);

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

    private void CaptureOriginalWindowStyle()
    {
        if (this._hasOriginalWindowStyle)
        {
            return;
        }

        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        this._originalWindowStyle = GetWindowLongPtr(windowHandle, GetWindowLongStyleIndex);
        this._hasOriginalWindowStyle = true;
    }

    private void RemoveWindowChrome()
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        nint windowStyle = GetWindowLongPtr(windowHandle, GetWindowLongStyleIndex);
        nint borderlessStyle = windowStyle & ~(WindowStyleCaption | WindowStyleThickFrame);
        _ = SetWindowLongPtr(windowHandle, GetWindowLongStyleIndex, borderlessStyle);
        RefreshWindowFrame(windowHandle);
    }

    private void RestoreOriginalWindowStyle()
    {
        if (!this._hasOriginalWindowStyle)
        {
            return;
        }

        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        _ = SetWindowLongPtr(windowHandle, GetWindowLongStyleIndex, this._originalWindowStyle);
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
        nint region = CreateRoundRectRgn(0, 0, widthPx, heightPx, radiusPx * 2, radiusPx * 2);
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
            this._windowController.Detach(this);
            this._shutdownComplete = true;
            this.Close();
        }
    }
}
