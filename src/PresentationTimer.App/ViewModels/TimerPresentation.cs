using System.Globalization;
using PresentationTimer.Core.Models;

namespace PresentationTimer.App.ViewModels;

internal enum TimerVisualState
{
    Normal,
    Warning,
    Overtime,
}

internal enum TimerPrimaryAction
{
    Start,
    Pause,
    Resume,
}

internal enum DurationPreset
{
    TenMinutes,
    FifteenMinutes,
    TwentyMinutes,
    ThirtyMinutes,
    Custom,
}

internal sealed record TimerPresentation
{
    public TimerPresentation(
        string displayText,
        string targetText,
        TimerVisualState visualState,
        TimerPrimaryAction primaryAction,
        double remainingRatio,
        bool isResetVisible,
        DurationPreset durationPreset)
    {
        this.DisplayText = displayText;
        this.TargetText = targetText;
        this.VisualState = visualState;
        this.PrimaryAction = primaryAction;
        this.RemainingRatio = remainingRatio;
        this.IsResetVisible = isResetVisible;
        this.DurationPreset = durationPreset;
    }

    public string DisplayText { get; }

    public string TargetText { get; }

    public TimerVisualState VisualState { get; }

    public TimerPrimaryAction PrimaryAction { get; }

    public double RemainingRatio { get; }

    public bool IsResetVisible { get; }

    public DurationPreset DurationPreset { get; }

    public static TimerPresentation FromSnapshot(TimerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        TimerVisualState visualState = snapshot.IsOvertime
            ? TimerVisualState.Overtime
            : snapshot.Remaining <= TimeSpan.FromMinutes(1)
                ? TimerVisualState.Warning
                : TimerVisualState.Normal;
        TimerPrimaryAction primaryAction = snapshot.RunState switch
        {
            TimerRunState.Running => TimerPrimaryAction.Pause,
            TimerRunState.Paused => TimerPrimaryAction.Resume,
            _ => TimerPrimaryAction.Start,
        };
        double remainingRatio = snapshot.Target <= TimeSpan.Zero
            ? 0
            : Math.Clamp(snapshot.Remaining.TotalSeconds / snapshot.Target.TotalSeconds, 0, 1);

        return new TimerPresentation(
            Format(snapshot.DisplayValue, snapshot.IsOvertime),
            Format(snapshot.Target, isOvertime: false),
            visualState,
            primaryAction,
            remainingRatio,
            snapshot.RunState != TimerRunState.Ready,
            ClassifyPreset(snapshot.Target));
    }

    private static DurationPreset ClassifyPreset(TimeSpan target) => target switch
    {
        { TotalMinutes: 10 } => DurationPreset.TenMinutes,
        { TotalMinutes: 15 } => DurationPreset.FifteenMinutes,
        { TotalMinutes: 20 } => DurationPreset.TwentyMinutes,
        { TotalMinutes: 30 } => DurationPreset.ThirtyMinutes,
        _ => DurationPreset.Custom,
    };

    private static string Format(TimeSpan value, bool isOvertime)
    {
        long totalSeconds = Math.Max(0, (long)value.TotalSeconds);
        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;

        if (isOvertime)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "+{0:00}:{1:00}:{2:00}",
                hours,
                minutes,
                seconds);
        }

        return hours > 0
            ? string.Format(CultureInfo.CurrentCulture, "{0}:{1:00}:{2:00}", hours, minutes, seconds)
            : string.Format(CultureInfo.CurrentCulture, "{0:00}:{1:00}", minutes, seconds);
    }
}
