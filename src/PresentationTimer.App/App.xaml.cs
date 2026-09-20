using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PresentationTimer.App.Localization;
using PresentationTimer.App.Logging;
using PresentationTimer.Core.Contracts;
using Serilog.Core;

namespace PresentationTimer.App;

/// <summary>
/// Provides application-specific behavior and owns process-lifetime services.
/// </summary>
public partial class App : Application
{
    private readonly object _shutdownGate = new object();
    private readonly Logger _processLogger;
    private AppCompositionRoot? _compositionRoot;
    private ServiceProvider? _services;
    private Task? _shutdownTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        LanguageManager.ApplySavedLanguage();
        this.InitializeComponent();
        this.HighContrastAdjustment = ApplicationHighContrastAdjustment.None;
        this._processLogger = LogBootstrapper.CreateLogger();
    }

    internal MainWindow? MainWindow { get; private set; }

    internal Task ShutdownAsync()
    {
        lock (this._shutdownGate)
        {
            this._shutdownTask ??= this.ShutdownCoreAsync();
            return this._shutdownTask;
        }
    }

    internal void ChangeLanguage(string languageTag)
    {
        if (this.MainWindow is not MainWindow currentWindow || this._services is null)
        {
            return;
        }

        string previousLanguage = LanguageManager.CurrentLanguageTag;
        if (string.Equals(previousLanguage, languageTag, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            LanguageManager.SetLanguage(languageTag);
            LocalizedStrings strings = this._services.GetRequiredService<LocalizedStrings>();
            strings.Reload();

            MainPage replacementPage = new (
                this._services.GetRequiredService<IPresentationSessionService>(),
                strings,
                this._services.GetRequiredService<WindowController>());
            MainWindow replacementWindow = new (
                replacementPage,
                strings,
                this._services.GetRequiredService<WindowController>(),
                this._services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MainWindow>>());
            Windows.Graphics.RectInt32 nativeWindowBounds = currentWindow.CopyWindowStateTo(replacementWindow);

            this.MainWindow = replacementWindow;
            replacementWindow.Activate();
            replacementWindow.RestoreNativeWindowBounds(nativeWindowBounds);
            currentWindow.CloseForLanguageChange();
        }
        catch (Exception exception)
        {
            LanguageManager.SetLanguage(previousLanguage);
            this._processLogger.Error(exception, "Language switch failed");
        }
    }

    /// <summary>
    /// Creates and activates the main presenter window.
    /// </summary>
    /// <param name="args">Details about the launch request.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            this._services ??= AppServiceProvider.Create(this._processLogger);
            this._compositionRoot = this._services.GetRequiredService<AppCompositionRoot>();
            this.MainWindow = this._services.GetRequiredService<MainWindow>();
            this.MainWindow.Activate();
            this._processLogger.Information("Application window activated");
            _ = this.StartServicesAsync(this._compositionRoot);
        }
        catch (Exception exception)
        {
            this._processLogger.Fatal(exception, "Application startup failed");
            throw;
        }
    }

    private async Task ShutdownCoreAsync()
    {
        try
        {
            if (this._compositionRoot is not null)
            {
                await this._compositionRoot.ShutdownAsync();
            }
        }
        catch (Exception exception)
        {
            this._processLogger.Error(exception, "Coordinated application shutdown failed");
        }
        finally
        {
            this._compositionRoot = null;
            this.MainWindow = null;

            if (this._services is not null)
            {
                await this._services.DisposeAsync();
                this._services = null;
            }

            this._processLogger.Information("Application shutdown completed");
            await this._processLogger.DisposeAsync();
        }
    }

    private async Task StartServicesAsync(AppCompositionRoot compositionRoot)
    {
        try
        {
            await compositionRoot.StartAsync();
            this._processLogger.Information("Presentation monitoring started");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            this._processLogger.Error(exception, "Presentation monitoring failed to start");
        }
    }
}
