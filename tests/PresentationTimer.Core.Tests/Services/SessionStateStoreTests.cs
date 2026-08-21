using System.Collections.Immutable;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Services;
using PresentationTimer.Core.Tests.Fakes;

namespace PresentationTimer.Core.Tests.Services;

/// <summary>
/// Tests immutable component-slice merging and revision behavior.
/// </summary>
[TestClass]
public sealed class SessionStateStoreTests
{
    /// <summary>
    /// Verifies each component update preserves newer values in every other slice.
    /// </summary>
    [TestMethod]
    public void UpdateSlices_SequentialChanges_PreservesOtherSlicesAndIncrementsRevision()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var store = CreateStore(timeProvider);
        var timer = new TimerSnapshot(TimerRunState.Running, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(14));
        var remote = new RemoteSessionPublicState(
            RemoteSessionStatus.Ready,
            ImmutableArray.Create(new Uri("http://192.168.1.2:5000/presenter")),
            new Uri("http://192.168.1.2:5000/presenter"),
            1,
            null);
        var presentation = new PresentationSnapshot(
            PresentationConnectionState.Running,
            2,
            10,
            "Notes",
            null);

        // Act
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        store.UpdateTimer(timer);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        store.UpdateRemote(remote);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        PresentationSessionState state = store.UpdatePresentation(presentation);

        // Assert
        Assert.AreEqual(3L, state.Revision);
        Assert.AreEqual(presentation, state.Presentation);
        Assert.AreEqual(timer, state.Timer);
        Assert.AreEqual(remote, state.Remote);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 21, 0, 0, 3, TimeSpan.Zero), state.ObservedAtUtc);
    }

    /// <summary>
    /// Verifies an equal non-timer snapshot is not rebroadcast or revisioned.
    /// </summary>
    [TestMethod]
    public void UpdatePresentation_WithEqualSnapshot_DoesNotPublishOrIncrementRevision()
    {
        // Arrange
        var store = CreateStore(new FakeTimeProvider());
        int publishCount = 0;
        store.StateChanged += _ => publishCount++;

        // Act
        PresentationSessionState state = store.UpdatePresentation(PresentationSnapshot.Initial);

        // Assert
        Assert.AreEqual(0, publishCount);
        Assert.AreEqual(0L, state.Revision);
    }

    /// <summary>
    /// Verifies state callbacks can safely initiate another component update.
    /// </summary>
    [TestMethod]
    public void StateChanged_CallbackUpdatesAnotherSlice_CommitsBothUpdates()
    {
        // Arrange
        var store = CreateStore(new FakeTimeProvider());
        var remote = RemoteSessionPublicState.Initial with { Status = RemoteSessionStatus.Starting };
        int publishCount = 0;
        store.StateChanged += _ =>
        {
            if (publishCount++ == 0)
            {
                store.UpdateRemote(remote);
            }
        };

        // Act
        store.UpdateTimer(new TimerSnapshot(
            TimerRunState.Running,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(15)));

        // Assert
        Assert.AreEqual(2, publishCount);
        Assert.AreEqual(2L, store.State.Revision);
        Assert.AreEqual(RemoteSessionStatus.Starting, store.State.Remote.Status);
    }

    private static SessionStateStore CreateStore(TimeProvider timeProvider) =>
        new (
            PresentationSnapshot.Initial,
            new TimerSnapshot(TimerRunState.Ready, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15)),
            RemoteSessionPublicState.Initial,
            timeProvider);
}
