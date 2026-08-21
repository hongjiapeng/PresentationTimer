using Microsoft.Extensions.Logging;

namespace PresentationTimer.Remote.Hosting;

/// <summary>
/// Forwards the embedded web host categories into the process logging pipeline.
/// </summary>
internal sealed class ForwardingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Initializes a new instance of the <see cref="ForwardingLoggerProvider"/> class.</summary>
    /// <param name="loggerFactory">The process-owned logger factory.</param>
    public ForwardingLoggerProvider(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        this._loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
        this._loggerFactory.CreateLogger(categoryName);

    /// <inheritdoc/>
    public void Dispose()
    {
        // The process container owns the forwarded logger factory.
    }
}
