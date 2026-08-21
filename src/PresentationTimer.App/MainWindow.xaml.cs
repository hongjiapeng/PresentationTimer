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
    private const int InitialHeight = 760;
    private const int InitialWidth = 1120;
    private const int MinimumHeight = 600;
    private const int MinimumWidth = 800;
    private readonly ILogger<MainWindow> _logger;
    private readonly MainPage _mainPage;
    private readonly WindowController _windowController;
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
        this.ConfigureWindowSize();
        this.AppWindow.Closing += this.OnClosing;
        this.RootFrame.Content = mainPage;
        this._windowController.Attach(this);
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

    [LoggerMessage(4000, LogLevel.Error, "Window shutdown encountered an error")]
    private static partial void LogWindowShutdownFailed(ILogger logger, Exception exception);

    private void ConfigureWindowSize()
    {
        nint windowHandle = Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
        double scale = GetDpiForWindow(windowHandle) / 96d;
        this.AppWindow.Resize(new SizeInt32(
            checked((int)(InitialWidth * scale)),
            checked((int)(InitialHeight * scale))));

        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = checked((int)(MinimumWidth * scale));
            presenter.PreferredMinimumHeight = checked((int)(MinimumHeight * scale));
        }
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
