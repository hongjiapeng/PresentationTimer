using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
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
    private const int ExpandedHeight = 680;
    private const int ExpandedWidth = 920;
    private const int MinimumExpandedHeight = 600;
    private const int MinimumExpandedWidth = 800;
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowCornerPreferenceDefault = 0;
    private const int DwmWindowCornerPreferenceRoundSmall = 3;
    private readonly ILogger<MainWindow> _logger;
    private readonly MainPage _mainPage;
    private readonly WindowController _windowController;
    private RectInt32? _compactBounds;
    private RectInt32? _expandedBounds;
    private bool _isCompactMode = true;
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
        this.SetTitleBar(this.AppTitleBar);
        this.AppWindow.SetIcon("Assets/AppIcon.ico");
        this.AppWindow.Closing += this.OnClosing;
        this.RootFrame.Content = mainPage;
        this._windowController.Attach(this);
        this.EnterCompactMode();
    }

    internal MainPage PresenterPage => this._mainPage;

    internal void EnterCompactMode()
    {
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (!this._isCompactMode)
        {
            this._expandedBounds = this.GetCurrentBounds();
        }

        this.AppTitleBar.Visibility = Visibility.Collapsed;
        this.SetTitleBar(this._mainPage.CompactDragRegionElement);
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.PreferredMinimumWidth = 0;
        presenter.PreferredMinimumHeight = 0;

        RectInt32 target = this._compactBounds ?? this.GetCurrentBounds();
        target.Width = this.ToPhysicalPixels(CompactWidth);
        target.Height = this.ToPhysicalPixels(CompactHeight);
        target = ClampToVisibleWorkArea(target);
        this.AppWindow.MoveAndResize(target);
        this._compactBounds = target;
        this._isCompactMode = true;
        this.RequestCornerPreference(DwmWindowCornerPreferenceRoundSmall);
    }

    internal void EnterExpandedMode()
    {
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (this._isCompactMode)
        {
            this._compactBounds = this.GetCurrentBounds();
        }

        presenter.SetBorderAndTitleBar(true, true);
        presenter.IsResizable = true;
        presenter.IsMaximizable = true;
        presenter.IsMinimizable = true;
        presenter.PreferredMinimumWidth = this.ToPhysicalPixels(MinimumExpandedWidth);
        presenter.PreferredMinimumHeight = this.ToPhysicalPixels(MinimumExpandedHeight);
        this.AppTitleBar.Visibility = Visibility.Visible;
        this.SetTitleBar(this.AppTitleBar);
        this.RequestCornerPreference(DwmWindowCornerPreferenceDefault);

        RectInt32 target = this._expandedBounds ?? this.GetCurrentBounds();
        if (this._expandedBounds is null)
        {
            target.Width = this.ToPhysicalPixels(ExpandedWidth);
            target.Height = this.ToPhysicalPixels(ExpandedHeight);
        }

        target = ClampToVisibleWorkArea(target);
        this.AppWindow.MoveAndResize(target);
        this._expandedBounds = target;

        this._isCompactMode = false;
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
            this._windowController.Detach(this);
            this._shutdownComplete = true;
            this.Close();
        }
    }
}
