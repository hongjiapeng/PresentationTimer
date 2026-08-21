using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PresentationTimer.App.Localization;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Services;
using PresentationTimer.Core.Timing;
using PresentationTimer.PowerPoint;
using PresentationTimer.Remote;
using Serilog;

namespace PresentationTimer.App;

/// <summary>
/// Defines the process dependency graph in one composition location.
/// </summary>
internal static class AppServiceProvider
{
    /// <summary>Builds and validates the process-lifetime dependency container.</summary>
    /// <param name="processLogger">The Serilog process logger.</param>
    /// <returns>The validated service provider.</returns>
    public static ServiceProvider Create(Serilog.ILogger processLogger)
    {
        ArgumentNullException.ThrowIfNull(processLogger);

        var services = new ServiceCollection();
        services.AddSingleton(processLogger);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(processLogger, dispose: false);
        });

        services.AddSingleton<IMonotonicClock, StopwatchMonotonicClock>();
        services.AddSingleton<IPresentationTimer, MonotonicPresentationTimer>();
        services.AddSingleton<PowerPointPresentationController>();
        services.AddSingleton<IPresentationController>(static provider =>
            provider.GetRequiredService<PowerPointPresentationController>());
        services.AddSingleton<RemoteSessionHost>(provider =>
            new RemoteSessionHost(
                () => provider.GetRequiredService<IPresentationSessionService>(),
                provider.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IRemoteSessionHost>(static provider =>
            provider.GetRequiredService<RemoteSessionHost>());
        services.AddSingleton<PresentationSessionService>();
        services.AddSingleton<IPresentationSessionService>(static provider =>
            provider.GetRequiredService<PresentationSessionService>());

        services.AddSingleton<LocalizedStrings>();
        services.AddSingleton<WindowController>();
        services.AddSingleton(static provider => new MainPage(
            provider.GetRequiredService<IPresentationSessionService>(),
            provider.GetRequiredService<LocalizedStrings>(),
            provider.GetRequiredService<WindowController>()));
        services.AddSingleton(static provider => new MainWindow(
            provider.GetRequiredService<MainPage>(),
            provider.GetRequiredService<LocalizedStrings>(),
            provider.GetRequiredService<WindowController>(),
            provider.GetRequiredService<ILogger<MainWindow>>()));
        services.AddSingleton<AppCompositionRoot>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
