using System.Runtime.InteropServices;
using SlidePace.Core.Models;
using SlidePace.PowerPoint.Interop;
using SlidePace.PowerPoint.Threading;
using PowerPointController = SlidePace.PowerPoint.PowerPointPresentationController;

namespace SlidePace.Core.Tests.PowerPoint;

/// <summary>
/// Verifies PowerPoint registration mapping and monitor readiness without requiring Office.
/// </summary>
[TestClass]
public sealed class PowerPointFoundationTests
{
    /// <summary>Verifies PowerPoint's temporary no-current-slide error is recognized.</summary>
    [TestMethod]
    public void PresentationSnapshotReader_NoSlideCurrentlyInView_IsTransientState()
    {
        // Arrange
        var exception = Marshal.GetExceptionForHR(unchecked((int)0x80048240)) as COMException
            ?? throw new InvalidOperationException("The HRESULT did not produce a COM exception.");

        // Act
        bool result = SlidePace.PowerPoint.Interop.PresentationSnapshotReader
            .IsNoSlideCurrentlyInView(exception);

        // Assert
        Assert.IsTrue(result);
    }

    /// <summary>Verifies unrelated COM errors are not classified as a temporary view state.</summary>
    [TestMethod]
    public void PresentationSnapshotReader_UnrelatedComError_IsNotTransientViewState()
    {
        // Arrange
        var exception = Marshal.GetExceptionForHR(unchecked((int)0x80010108)) as COMException
            ?? throw new InvalidOperationException("The HRESULT did not produce a COM exception.");

        // Act
        bool result = SlidePace.PowerPoint.Interop.PresentationSnapshotReader
            .IsNoSlideCurrentlyInView(exception);

        // Assert
        Assert.IsFalse(result);
    }

    /// <summary>Verifies an unknown ProgID maps to the unavailable category.</summary>
    [TestMethod]
    public void ActiveObjectResolver_WithUnknownProgId_ReturnsUnavailable()
    {
        // Arrange
        var resolver = new ActiveObjectResolver();

        // Act
        ActiveObjectResult result = resolver.Resolve($"SlidePace.Unknown.{Guid.NewGuid():N}");

        // Assert
        Assert.AreEqual(ActiveObjectStatus.Unavailable, result.Status);
        Assert.IsNull(result.Instance);
        Assert.AreNotEqual(0, result.HResult);
    }

    /// <summary>Verifies an unavailable installation is published without throwing.</summary>
    /// <returns>A task that completes after verification.</returns>
    [TestMethod]
    public async Task StartMonitoringAsync_WhenPowerPointUnavailable_PublishesUnavailableState()
    {
        // Arrange
        var resolver = new StubActiveObjectResolver(ActiveObjectStatus.Unavailable);
        await using var dispatcher = new StaComDispatcher();
        await using var controller = new PowerPointController(
            resolver,
            dispatcher,
            TimeSpan.FromMinutes(1));

        // Act
        await controller.StartMonitoringAsync();

        // Assert
        Assert.AreEqual(PresentationConnectionState.Unavailable, controller.State.Connection);
        Assert.IsNull(controller.State.CurrentSlideIndex);
        Assert.AreEqual(string.Empty, controller.State.SpeakerNotes);
    }

    /// <summary>Verifies a registered but inactive server maps to not-running state.</summary>
    /// <returns>A task that completes after verification.</returns>
    [TestMethod]
    public async Task StartMonitoringAsync_WhenPowerPointNotRunning_PublishesNotRunningState()
    {
        // Arrange
        var resolver = new StubActiveObjectResolver(ActiveObjectStatus.NotRunning);
        await using var dispatcher = new StaComDispatcher();
        await using var controller = new PowerPointController(
            resolver,
            dispatcher,
            TimeSpan.FromMinutes(1));

        // Act
        await controller.StartMonitoringAsync();

        // Assert
        Assert.AreEqual(PresentationConnectionState.NotRunning, controller.State.Connection);
        Assert.IsNull(controller.State.LastErrorCode);
    }

    /// <summary>Verifies transient COM busy failures are retried within the configured bound.</summary>
    /// <returns>A task that completes after the successful retry.</returns>
    [TestMethod]
    public async Task ExecuteWithBusyRetryAsync_TwoBusyResults_ThirdAttemptSucceeds()
    {
        // Arrange
        int attempts = 0;

        // Act
        SlidePace.Core.Results.OperationResult result =
            await PowerPointController.ExecuteWithBusyRetryAsync(
                _ => Task.FromResult(++attempts < 3
                    ? SlidePace.Core.Results.OperationResult.Failure(
                        SlidePace.Core.Results.ErrorCodes.PresentationBusy,
                        "busy")
                    : SlidePace.Core.Results.OperationResult.Success()),
                attemptLimit: 3,
                retryDelay: TimeSpan.Zero);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(3, attempts);
    }

    /// <summary>Verifies persistent COM busy failures stop at the configured attempt limit.</summary>
    /// <returns>A task that completes after the bounded attempts.</returns>
    [TestMethod]
    public async Task ExecuteWithBusyRetryAsync_AlwaysBusy_ReturnsBusyAtAttemptLimit()
    {
        // Arrange
        int attempts = 0;

        // Act
        SlidePace.Core.Results.OperationResult result =
            await PowerPointController.ExecuteWithBusyRetryAsync(
                _ =>
                {
                    attempts++;
                    return Task.FromResult(SlidePace.Core.Results.OperationResult.Failure(
                        SlidePace.Core.Results.ErrorCodes.PresentationBusy,
                        "busy"));
                },
                attemptLimit: 3,
                retryDelay: TimeSpan.Zero);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(SlidePace.Core.Results.ErrorCodes.PresentationBusy, result.ErrorCode);
        Assert.AreEqual(3, attempts);
    }

    /// <summary>Verifies cancellation interrupts the delay before another COM submission.</summary>
    /// <returns>A task that completes after cancellation.</returns>
    [TestMethod]
    public async Task ExecuteWithBusyRetryAsync_CancelledAfterFirstBusy_DoesNotSubmitAgain()
    {
        // Arrange
        int attempts = 0;
        using var cancellation = new CancellationTokenSource();

        // Act and assert
        _ = await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await PowerPointController.ExecuteWithBusyRetryAsync(
                _ =>
                {
                    attempts++;
                    cancellation.Cancel();
                    return Task.FromResult(SlidePace.Core.Results.OperationResult.Failure(
                        SlidePace.Core.Results.ErrorCodes.PresentationBusy,
                        "busy"));
                },
                attemptLimit: 3,
                retryDelay: TimeSpan.FromMinutes(1),
                cancellation.Token));
        Assert.AreEqual(1, attempts);
    }

    private sealed class StubActiveObjectResolver : IActiveObjectResolver
    {
        private readonly ActiveObjectStatus _status;

        public StubActiveObjectResolver(ActiveObjectStatus status)
        {
            this._status = status;
        }

        public ActiveObjectResult Resolve(string programmaticIdentifier) =>
            new ActiveObjectResult(this._status, null, unchecked((int)0x800401E3));
    }
}
