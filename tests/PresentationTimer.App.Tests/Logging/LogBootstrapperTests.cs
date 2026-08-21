using System.Globalization;
using System.Text.Json;
using PresentationTimer.App.Logging;
using Serilog.Core;

namespace PresentationTimer.App.Tests.Logging;

/// <summary>Verifies the local Serilog file policy and structured output.</summary>
[TestClass]
public sealed class LogBootstrapperTests
{
    /// <summary>Verifies logs are JSON and use bounded size, count, and age retention.</summary>
    [TestMethod]
    public void CreateLogger_LocalDirectory_WritesStructuredJsonWithBoundedPolicy()
    {
        // Arrange
        string directory = Path.Combine(
            Path.GetTempPath(),
            string.Concat("PresentationTimer-Logs-", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));

        try
        {
            // Act
            using (Logger logger = LogBootstrapper.CreateLogger(directory))
            {
                logger.Information("Logging policy probe {Probe}", 42);
            }

            string logFile = Assert.ContainsSingle(Directory.GetFiles(directory, "*.json"));
            string firstLine = File.ReadLines(logFile).First();
            using JsonDocument document = JsonDocument.Parse(firstLine);

            // Assert
            Assert.AreEqual(5 * 1024 * 1024, LogBootstrapper.FileSizeLimitBytes);
            Assert.AreEqual(7, LogBootstrapper.RetainedFileCount);
            Assert.AreEqual(TimeSpan.FromDays(7), LogBootstrapper.RetainedFileTimeLimit);
            Assert.AreEqual(
                "Logging policy probe 42",
                document.RootElement.GetProperty("RenderedMessage").GetString());
            Assert.AreEqual(
                42,
                document.RootElement.GetProperty("Properties").GetProperty("Probe").GetInt32());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
