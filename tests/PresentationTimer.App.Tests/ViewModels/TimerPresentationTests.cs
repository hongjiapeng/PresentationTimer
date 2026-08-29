using PresentationTimer.App.ViewModels;
using PresentationTimer.Core.Models;

namespace PresentationTimer.App.Tests.ViewModels;

/// <summary>Verifies the pure desktop timer presentation projection.</summary>
[TestClass]
public sealed class TimerPresentationTests
{
    /// <summary>Verifies each domain run state maps to one primary action and the correct Reset visibility.</summary>
    /// <param name="runState">The authoritative domain run state.</param>
    /// <param name="expectedAction">The expected presentation action name.</param>
    /// <param name="expectedResetVisible">Whether Reset should be visible.</param>
    [TestMethod]
    [DataRow(TimerRunState.Ready, "Start", false)]
    [DataRow(TimerRunState.Running, "Pause", true)]
    [DataRow(TimerRunState.Paused, "Resume", true)]
    public void FromSnapshot_RunState_SelectsPrimaryActionAndReset(
        TimerRunState runState,
        string expectedAction,
        bool expectedResetVisible)
    {
        var snapshot = new TimerSnapshot(runState, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

        TimerPresentation result = TimerPresentation.FromSnapshot(snapshot);

        Assert.AreEqual(expectedAction, result.PrimaryAction.ToString());
        Assert.AreEqual(expectedResetVisible, result.IsResetVisible);
        Assert.AreEqual("15:00", result.DisplayText);
    }

    /// <summary>Verifies the normal, exact warning-boundary, and zero countdown states.</summary>
    /// <param name="remainingSeconds">The signed remaining seconds.</param>
    /// <param name="expectedState">The expected presentation state name.</param>
    /// <param name="expectedText">The expected countdown text.</param>
    [TestMethod]
    [DataRow(901, "Normal", "15:01")]
    [DataRow(60, "Warning", "01:00")]
    [DataRow(0, "Warning", "00:00")]
    public void FromSnapshot_NonNegativeRemaining_SelectsExpectedVisualState(
        int remainingSeconds,
        string expectedState,
        string expectedText)
    {
        var snapshot = new TimerSnapshot(
            TimerRunState.Running,
            TimeSpan.FromMinutes(20),
            TimeSpan.FromSeconds(remainingSeconds));

        TimerPresentation result = TimerPresentation.FromSnapshot(snapshot);

        Assert.AreEqual(expectedState, result.VisualState.ToString());
        Assert.AreEqual(expectedText, result.DisplayText);
    }

    /// <summary>Verifies overtime text remains unambiguous for seconds, minutes, and multiple hours.</summary>
    /// <param name="remainingSeconds">The signed remaining seconds.</param>
    /// <param name="expectedText">The expected overtime text.</param>
    [TestMethod]
    [DataRow(-1, "+00:00:01")]
    [DataRow(-92, "+00:01:32")]
    [DataRow(-10861, "+03:01:01")]
    public void FromSnapshot_Overtime_UsesLeadingPlusAndHourFields(
        int remainingSeconds,
        string expectedText)
    {
        var snapshot = new TimerSnapshot(
            TimerRunState.Running,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromSeconds(remainingSeconds));

        TimerPresentation result = TimerPresentation.FromSnapshot(snapshot);

        Assert.AreEqual(TimerVisualState.Overtime, result.VisualState);
        Assert.AreEqual(expectedText, result.DisplayText);
        Assert.AreEqual(0d, result.RemainingRatio);
    }

    /// <summary>Verifies remaining progress is bounded even for invalid or out-of-range snapshots.</summary>
    /// <param name="remainingSeconds">The signed remaining seconds.</param>
    /// <param name="targetSeconds">The configured target seconds.</param>
    /// <param name="expectedRatio">The expected clamped ratio.</param>
    [TestMethod]
    [DataRow(1200, 1200, 1d)]
    [DataRow(600, 1200, 0.5d)]
    [DataRow(1800, 1200, 1d)]
    [DataRow(-1, 1200, 0d)]
    [DataRow(0, 0, 0d)]
    public void FromSnapshot_RemainingRatio_IsClamped(
        int remainingSeconds,
        int targetSeconds,
        double expectedRatio)
    {
        var snapshot = new TimerSnapshot(
            TimerRunState.Running,
            TimeSpan.FromSeconds(targetSeconds),
            TimeSpan.FromSeconds(remainingSeconds));

        TimerPresentation result = TimerPresentation.FromSnapshot(snapshot);

        Assert.AreEqual(expectedRatio, result.RemainingRatio, 0.0001d);
    }

    /// <summary>Verifies supported quick durations and custom targets are classified distinctly.</summary>
    /// <param name="targetSeconds">The configured target seconds.</param>
    /// <param name="expectedPreset">The expected preset name.</param>
    [TestMethod]
    [DataRow(600, "TenMinutes")]
    [DataRow(900, "FifteenMinutes")]
    [DataRow(1200, "TwentyMinutes")]
    [DataRow(1800, "ThirtyMinutes")]
    [DataRow(3930, "Custom")]
    public void FromSnapshot_Target_ClassifiesPreset(
        int targetSeconds,
        string expectedPreset)
    {
        var snapshot = new TimerSnapshot(
            TimerRunState.Ready,
            TimeSpan.FromSeconds(targetSeconds),
            TimeSpan.FromSeconds(targetSeconds));

        TimerPresentation result = TimerPresentation.FromSnapshot(snapshot);

        Assert.AreEqual(expectedPreset, result.DurationPreset.ToString());
    }

    /// <summary>Verifies non-overtime multi-hour countdowns retain the existing compact format.</summary>
    [TestMethod]
    public void FromSnapshot_MultiHourCountdown_UsesUnpaddedHour()
    {
        var snapshot = new TimerSnapshot(
            TimerRunState.Ready,
            new TimeSpan(3, 5, 7),
            new TimeSpan(3, 5, 7));

        TimerPresentation result = TimerPresentation.FromSnapshot(snapshot);

        Assert.AreEqual("3:05:07", result.DisplayText);
        Assert.AreEqual("3:05:07", result.TargetText);
    }
}
