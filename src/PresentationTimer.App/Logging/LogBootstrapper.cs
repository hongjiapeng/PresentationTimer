using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace PresentationTimer.App.Logging;

/// <summary>
/// Creates the process logger with bounded, structured local storage.
/// </summary>
internal static class LogBootstrapper
{
    internal static long FileSizeLimitBytes { get; } = 5 * 1024 * 1024;

    internal static int RetainedFileCount { get; } = 7;

    internal static TimeSpan RetainedFileTimeLimit { get; } = TimeSpan.FromDays(7);

    /// <summary>Creates the process-lifetime Serilog logger.</summary>
    /// <param name="logDirectory">An optional testable log directory override.</param>
    /// <returns>A logger that the application must dispose during shutdown.</returns>
    public static Logger CreateLogger(string? logDirectory = null)
    {
        logDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PresentationTimer",
            "Logs");
        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "PresentationTimer")
            .WriteTo.File(
                new JsonFormatter(renderMessage: true),
                Path.Combine(logDirectory, "presentation-timer-.json"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: FileSizeLimitBytes,
                retainedFileCountLimit: RetainedFileCount,
                retainedFileTimeLimit: RetainedFileTimeLimit,
                flushToDiskInterval: TimeSpan.FromSeconds(2))
            .CreateLogger();
    }
}
