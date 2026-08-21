using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;
using PresentationTimer.Core.Tests.Fakes;
using PresentationTimer.Core.Timing;

namespace PresentationTimer.Core.Tests.Timing;

/// <summary>
/// Tests authoritative countdown and overtime behavior.
/// </summary>
[TestClass]
public sealed class MonotonicPresentationTimerTests
{
    /// <summary>
    /// Verifies configuration and the ready-to-running transition.
    /// </summary>
    [TestMethod]
    public void ConfigureAndStart_FifteenMinutes_SetsTargetAndRuns()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = new MonotonicPresentationTimer(clock);

        // Act
        OperationResult<TimerSnapshot> configured = timer.Configure(TimeSpan.FromMinutes(15));
        OperationResult<TimerSnapshot> started = timer.Start();

        // Assert
        Assert.IsTrue(configured.IsSuccess);
        Assert.AreEqual(TimerRunState.Ready, configured.Value?.RunState);
        Assert.AreEqual(TimeSpan.FromMinutes(15), configured.Value?.Target);
        Assert.AreEqual(TimeSpan.FromMinutes(15), configured.Value?.Remaining);
        Assert.IsTrue(started.IsSuccess);
        Assert.AreEqual(TimerRunState.Running, started.Value?.RunState);
    }

    /// <summary>
    /// Verifies invalid duration values leave the last valid target unchanged.
    /// </summary>
    /// <param name="ticks">The invalid duration in ticks.</param>
    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(10000001L)]
    [DataRow(3600000000000L)]
    public void Configure_InvalidDuration_RejectsAndPreservesTarget(long ticks)
    {
        // Arrange
        var timer = new MonotonicPresentationTimer(new FakeMonotonicClock());
        timer.Configure(TimeSpan.FromMinutes(15));

        // Act
        OperationResult<TimerSnapshot> result = timer.Configure(TimeSpan.FromTicks(ticks));

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.InvalidDuration, result.ErrorCode);
        Assert.AreEqual(TimeSpan.FromMinutes(15), timer.State.Target);
    }

    /// <summary>
    /// Verifies a twenty-second notification stall is included in elapsed time.
    /// </summary>
    [TestMethod]
    public void Snapshot_AfterTwentySecondDisplayStall_IncludesEntireStall()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = CreateRunningTimer(clock, TimeSpan.FromMinutes(15));
        clock.Advance(TimeSpan.FromMinutes(10));
        _ = timer.Snapshot();

        // Act
        clock.Advance(TimeSpan.FromSeconds(20));
        TimerSnapshot snapshot = timer.Snapshot();

        // Assert
        Assert.AreEqual(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(40), snapshot.Remaining);
    }

    /// <summary>
    /// Verifies paused countdown time does not accumulate and resume continues from the preserved value.
    /// </summary>
    [TestMethod]
    public void PauseAndResume_Countdown_PreservesPausedTime()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = CreateRunningTimer(clock, TimeSpan.FromMinutes(15));
        clock.Advance(TimeSpan.FromMinutes(7));

        // Act
        timer.Pause();
        clock.Advance(TimeSpan.FromSeconds(30));
        TimerSnapshot paused = timer.Snapshot();
        timer.ResumeTimer();
        clock.Advance(TimeSpan.FromSeconds(1));
        TimerSnapshot resumed = timer.Snapshot();

        // Assert
        Assert.AreEqual(TimeSpan.FromMinutes(8), paused.Remaining);
        Assert.AreEqual(TimerRunState.Paused, paused.RunState);
        Assert.AreEqual(TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(59), resumed.Remaining);
        Assert.AreEqual(TimerRunState.Running, resumed.RunState);
    }

    /// <summary>
    /// Verifies overtime remains fixed while paused and increases after resume.
    /// </summary>
    [TestMethod]
    public void PauseAndResume_Overtime_PreservesAndContinuesOvertime()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = CreateRunningTimer(clock, TimeSpan.FromMinutes(1));
        clock.Advance(TimeSpan.FromMinutes(3));

        // Act
        timer.Pause();
        clock.Advance(TimeSpan.FromSeconds(30));
        TimerSnapshot paused = timer.Snapshot();
        timer.ResumeTimer();
        clock.Advance(TimeSpan.FromSeconds(10));
        TimerSnapshot resumed = timer.Snapshot();

        // Assert
        Assert.IsTrue(paused.IsOvertime);
        Assert.AreEqual(TimeSpan.FromMinutes(2), paused.DisplayValue);
        Assert.AreEqual(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(10), resumed.DisplayValue);
    }

    /// <summary>
    /// Verifies reset works from every ready, running, paused, and overtime combination.
    /// </summary>
    /// <param name="scenario">The timer state prepared before reset.</param>
    [TestMethod]
    [DataRow("ready")]
    [DataRow("running")]
    [DataRow("paused")]
    [DataRow("running-overtime")]
    [DataRow("paused-overtime")]
    public void Reset_FromEverySupportedState_RestoresConfiguredTarget(string scenario)
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = new MonotonicPresentationTimer(clock);
        timer.Configure(TimeSpan.FromSeconds(2));
        PrepareScenario(timer, clock, scenario);

        // Act
        OperationResult<TimerSnapshot> result = timer.Reset();
        clock.Advance(TimeSpan.FromMinutes(1));
        TimerSnapshot afterWaiting = timer.Snapshot();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(TimerRunState.Ready, result.Value?.RunState);
        Assert.AreEqual(TimeSpan.FromSeconds(2), result.Value?.Remaining);
        Assert.IsFalse(result.Value?.IsOvertime ?? true);
        Assert.AreEqual(TimeSpan.FromSeconds(2), afterWaiting.Remaining);
    }

    /// <summary>
    /// Verifies the running timer crosses zero without stopping.
    /// </summary>
    [TestMethod]
    public void Snapshot_CrossesZero_RemainsRunningAndReportsOvertime()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = CreateRunningTimer(clock, TimeSpan.FromSeconds(1));

        // Act
        clock.Advance(TimeSpan.FromSeconds(1));
        TimerSnapshot atZero = timer.Snapshot();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        TimerSnapshot overtime = timer.Snapshot();

        // Assert
        Assert.AreEqual(TimeSpan.Zero, atZero.Remaining);
        Assert.IsFalse(atZero.IsOvertime);
        Assert.AreEqual(TimerRunState.Running, overtime.RunState);
        Assert.IsTrue(overtime.IsOvertime);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), overtime.DisplayValue);
    }

    /// <summary>
    /// Verifies long elapsed durations remain accurate without overflow or stopping.
    /// </summary>
    [TestMethod]
    public void Snapshot_AfterLongElapsedDuration_ReportsAccurateOvertime()
    {
        // Arrange
        var clock = new FakeMonotonicClock();
        var timer = CreateRunningTimer(clock, TimeSpan.FromMinutes(1));

        // Act
        clock.Advance(TimeSpan.FromHours(100));
        TimerSnapshot snapshot = timer.Snapshot();

        // Assert
        Assert.AreEqual(TimerRunState.Running, snapshot.RunState);
        Assert.AreEqual(TimeSpan.FromHours(99) + TimeSpan.FromMinutes(59), snapshot.DisplayValue);
    }

    private static MonotonicPresentationTimer CreateRunningTimer(
        FakeMonotonicClock clock,
        TimeSpan target)
    {
        var timer = new MonotonicPresentationTimer(clock);
        timer.Configure(target);
        timer.Start();
        return timer;
    }

    private static void PrepareScenario(
        MonotonicPresentationTimer timer,
        FakeMonotonicClock clock,
        string scenario)
    {
        if (scenario == "ready")
        {
            return;
        }

        timer.Start();
        clock.Advance(scenario.Contains("overtime", StringComparison.Ordinal)
            ? TimeSpan.FromSeconds(3)
            : TimeSpan.FromSeconds(1));

        if (scenario.StartsWith("paused", StringComparison.Ordinal))
        {
            timer.Pause();
        }
    }
}
