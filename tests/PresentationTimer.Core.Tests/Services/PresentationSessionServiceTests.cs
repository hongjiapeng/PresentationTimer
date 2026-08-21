using System.Collections.Immutable;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;
using PresentationTimer.Core.Services;
using PresentationTimer.Core.Tests.Fakes;
using PresentationTimer.Core.Timing;

namespace PresentationTimer.Core.Tests.Services;

/// <summary>
/// Tests coordination through the single presentation-session command gateway.
/// </summary>
[TestClass]
public sealed class PresentationSessionServiceTests
{
    /// <summary>
    /// Verifies a later presentation event cannot overwrite newer timer and remote slices.
    /// </summary>
    [TestMethod]
    public void PresentationEvent_AfterTimerAndRemoteChanges_PreservesNewerSlices()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = new MonotonicPresentationTimer(clock);
        var presentation = new FakePresentationController();
        var remote = new FakeRemoteSessionHost();
        using var service = new PresentationSessionService(timer, presentation, remote);
        service.ConfigureTimer("15:00");
        service.StartTimer();
        clock.Advance(TimeSpan.FromMinutes(1));
        TimerSnapshot timerBeforePresentation = service.RefreshTimer();
        var remoteBeforePresentation = new RemoteSessionPublicState(
            RemoteSessionStatus.Ready,
            ImmutableArray.Create(new Uri("http://192.168.1.2:5000/presenter")),
            new Uri("http://192.168.1.2:5000/presenter"),
            1,
            null);
        remote.Publish(remoteBeforePresentation);

        // Act
        presentation.Publish(new PresentationSnapshot(
            PresentationConnectionState.Running,
            4,
            12,
            "Current notes",
            null));

        // Assert
        Assert.AreEqual(timerBeforePresentation, service.State.Timer);
        Assert.AreEqual(remoteBeforePresentation, service.State.Remote);
        Assert.AreEqual(4, service.State.Presentation.CurrentSlideIndex);
    }

    /// <summary>
    /// Verifies a disconnect removes old slide position and notes before publication.
    /// </summary>
    [TestMethod]
    public void PresentationEvent_Disconnected_ClearsStalePresentationFields()
    {
        // Arrange
        var presentation = new FakePresentationController();
        using var service = CreateService(presentation);
        presentation.Publish(new PresentationSnapshot(
            PresentationConnectionState.Running,
            5,
            20,
            "Private notes",
            null));

        // Act
        presentation.Publish(new PresentationSnapshot(
            PresentationConnectionState.Disconnected,
            5,
            20,
            "Private notes",
            "presentation.rpc_disconnected"));

        // Assert
        Assert.AreEqual(PresentationConnectionState.Disconnected, service.State.Presentation.Connection);
        Assert.IsNull(service.State.Presentation.CurrentSlideIndex);
        Assert.IsNull(service.State.Presentation.TotalSlides);
        Assert.AreEqual(string.Empty, service.State.Presentation.SpeakerNotes);
        Assert.AreEqual("presentation.rpc_disconnected", service.State.Presentation.LastErrorCode);
    }

    /// <summary>
    /// Verifies each navigation command reaches the infrastructure adapter exactly once.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [TestMethod]
    public async Task NavigationCommands_InvokedOnce_RouteExactlyOnce()
    {
        // Arrange
        var presentation = new FakePresentationController();
        using var service = CreateService(presentation);

        // Act
        await service.NextSlideAsync();
        await service.PreviousSlideAsync();

        // Assert
        Assert.AreEqual(1, presentation.NextInvocationCount);
        Assert.AreEqual(1, presentation.PreviousInvocationCount);
    }

    /// <summary>
    /// Verifies malformed duration input cannot change the last valid target.
    /// </summary>
    [TestMethod]
    public void ConfigureTimer_MalformedInput_PreservesLastValidTarget()
    {
        // Arrange
        using PresentationSessionService service = CreateService(new FakePresentationController());
        service.ConfigureTimer("15:00");

        // Act
        var result = service.ConfigureTimer("not-time");

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(TimeSpan.FromMinutes(15), service.State.Timer.Target);
    }

    /// <summary>Verifies coordinated shutdown rejects every new command before infrastructure cleanup.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [TestMethod]
    public async Task BeginShutdown_NewCommands_ReturnClosingFailureWithoutInfrastructureCalls()
    {
        // Arrange
        var presentation = new FakePresentationController();
        var remote = new FakeRemoteSessionHost();
        using var service = new PresentationSessionService(
            new MonotonicPresentationTimer(new FakeMonotonicClock()),
            presentation,
            remote);
        service.ConfigureTimer("15:00");

        // Act
        service.BeginShutdown();
        service.BeginShutdown();
        OperationResult<TimerSnapshot> timerResult = service.StartTimer();
        OperationResult nextResult = await service.NextSlideAsync();
        OperationResult previousResult = await service.PreviousSlideAsync();
        OperationResult<DesktopPairingDescriptor> remoteResult = await service.StartRemoteSessionAsync();

        // Assert
        Assert.AreEqual(ErrorCodes.ApplicationClosing, timerResult.ErrorCode);
        Assert.AreEqual(ErrorCodes.ApplicationClosing, nextResult.ErrorCode);
        Assert.AreEqual(ErrorCodes.ApplicationClosing, previousResult.ErrorCode);
        Assert.AreEqual(ErrorCodes.ApplicationClosing, remoteResult.ErrorCode);
        Assert.AreEqual(0, presentation.NextInvocationCount);
        Assert.AreEqual(0, presentation.PreviousInvocationCount);
        Assert.AreEqual(0, remote.StartInvocationCount);
    }

    /// <summary>
    /// Verifies subsecond refreshes do not publish more than one state per displayed second.
    /// </summary>
    [TestMethod]
    public void RefreshTimer_MultipleSubsecondRefreshes_PublishesOnlyDisplayedSecondChanges()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = new MonotonicPresentationTimer(clock);
        using var service = new PresentationSessionService(
            timer,
            new FakePresentationController(),
            new FakeRemoteSessionHost());
        service.ConfigureTimer("15:00");
        service.StartTimer();
        int publishCount = 0;
        service.StateChanged += _ => publishCount++;

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(100));
        service.RefreshTimer();
        clock.Advance(TimeSpan.FromMilliseconds(400));
        service.RefreshTimer();
        clock.Advance(TimeSpan.FromMilliseconds(500));
        service.RefreshTimer();

        // Assert
        Assert.AreEqual(1, publishCount);
        Assert.AreEqual(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59), service.State.Timer.Remaining);
    }

    private static PresentationSessionService CreateService(FakePresentationController presentation) =>
        new (
            new MonotonicPresentationTimer(new FakeMonotonicClock()),
            presentation,
            new FakeRemoteSessionHost());
}
