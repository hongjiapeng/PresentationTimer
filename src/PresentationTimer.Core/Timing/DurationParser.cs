using System.Globalization;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Timing;

/// <summary>
/// Parses presenter-friendly whole-second duration text.
/// </summary>
public static class DurationParser
{
    private const int MaximumHours = 99;

    /// <summary>
    /// Gets the largest duration accepted by the MVP display and input contract.
    /// </summary>
    public static TimeSpan MaximumDuration { get; } = new (MaximumHours, 59, 59);

    /// <summary>
    /// Parses minutes, minutes:seconds, or hours:minutes:seconds using invariant digits.
    /// </summary>
    /// <param name="text">The user-entered duration.</param>
    /// <returns>The parsed duration or a stable validation failure.</returns>
    public static OperationResult<TimeSpan> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid();
        }

        string[] parts = text.Trim().Split(':');
        if (parts.Length is < 1 or > 3 || parts.Any(part => part.Length == 0))
        {
            return Invalid();
        }

        if (!parts.All(part => int.TryParse(
                part,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _)))
        {
            return Invalid();
        }

        int hours = 0;
        int minutes;
        int seconds = 0;

        if (parts.Length == 1)
        {
            minutes = int.Parse(parts[0], CultureInfo.InvariantCulture);
        }
        else if (parts.Length == 2)
        {
            minutes = int.Parse(parts[0], CultureInfo.InvariantCulture);
            seconds = int.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        else
        {
            hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
            minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
            seconds = int.Parse(parts[2], CultureInfo.InvariantCulture);
        }

        if (hours > MaximumHours || minutes < 0 || seconds < 0 || seconds > 59 ||
            (parts.Length == 3 && minutes > 59))
        {
            return Invalid();
        }

        TimeSpan duration;
        try
        {
            duration = parts.Length == 3
                ? new TimeSpan(hours, minutes, seconds)
                : TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        }
        catch (OverflowException)
        {
            return Invalid();
        }

        return IsSupported(duration)
            ? OperationResult.Success(duration)
            : Invalid();
    }

    /// <summary>
    /// Determines whether a duration is positive, whole-second, and displayable.
    /// </summary>
    /// <param name="duration">The duration to validate.</param>
    /// <returns><see langword="true"/> when the duration is supported.</returns>
    public static bool IsSupported(TimeSpan duration) =>
        duration > TimeSpan.Zero &&
        duration <= MaximumDuration &&
        duration.Ticks % TimeSpan.TicksPerSecond == 0;

    private static OperationResult<TimeSpan> Invalid() =>
        OperationResult.Failure<TimeSpan>(
            ErrorCodes.InvalidDuration,
            "Enter a positive whole-second duration up to 99:59:59.");
}
