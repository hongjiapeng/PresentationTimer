using Microsoft.Extensions.Logging;
using PresentationTimer.Core.Contracts;

namespace PresentationTimer.App;

internal sealed partial class AppCompositionRoot
{
    private readonly ILogger<AppCompositionRoot> _logger;
    private readonly IPresentationController _presentationController;
    private readonly IRemoteSessionHost _remoteSessionHost;
    private readonly IPresentationSessionService _sessionService;
    private readonly WindowController _windowController;
    private readonly object _shutdownGate = new object();
    private Task? _shutdownTask;

    public AppCompositionRoot(
        IPresentationSessionService sessionService,
        IPresentationController presentationController,
        IRemoteSessionHost remoteSessionHost,
        WindowController windowController,
        ILogger<AppCompositionRoot> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(presentationController);
        ArgumentNullException.ThrowIfNull(remoteSessionHost);
        ArgumentNullException.ThrowIfNull(windowController);
        ArgumentNullException.ThrowIfNull(logger);
        this._sessionService = sessionService;
        this._presentationController = presentationController;
        this._remoteSessionHost = remoteSessionHost;
        this._windowController = windowController;
        this._logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        this._sessionService.StartPresentationMonitoringAsync(cancellationToken);

    public Task ShutdownAsync()
    {
        lock (this._shutdownGate)
        {
            this._shutdownTask ??= this.ShutdownCoreAsync();
            return this._shutdownTask;
        }
    }

    [LoggerMessage(3000, LogLevel.Warning, "Remote presenter shutdown failed; cleanup is continuing")]
    private static partial void LogRemoteShutdownFailed(ILogger logger, Exception exception);

    [LoggerMessage(3001, LogLevel.Warning, "PowerPoint shutdown failed; cleanup is continuing")]
    private static partial void LogPowerPointShutdownFailed(ILogger logger, Exception exception);

    [LoggerMessage(3002, LogLevel.Warning, "UI notification shutdown failed; cleanup is continuing")]
    private static partial void LogUiShutdownFailed(ILogger logger, Exception exception);

    private async Task ShutdownCoreAsync()
    {
        this._sessionService.BeginShutdown();

        try
        {
            using var remoteTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await this._remoteSessionHost.StopAsync(remoteTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogRemoteShutdownFailed(this._logger, exception);
        }

        try
        {
            await this._windowController.StopUiNotificationsAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogUiShutdownFailed(this._logger, exception);
        }

        this._sessionService.DetachEvents();

        try
        {
            using var powerPointTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await this._presentationController.StopMonitoringAsync(powerPointTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogPowerPointShutdownFailed(this._logger, exception);
        }
    }
}
