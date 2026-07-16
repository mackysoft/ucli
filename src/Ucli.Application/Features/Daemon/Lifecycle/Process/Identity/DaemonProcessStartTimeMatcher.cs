namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;

/// <summary> Compares daemon process start timestamps with the tolerance required by operating-system timestamp precision. </summary>
internal static class DaemonProcessStartTimeMatcher
{
    private static readonly TimeSpan WholeSecondTimestampPrecision = TimeSpan.FromSeconds(1);

    /// <summary> Gets the maximum accepted delta between two observations of one process start timestamp. </summary>
    public static TimeSpan Tolerance { get; } = WholeSecondTimestampPrecision * 2;

    /// <summary> Determines whether two daemon process start timestamps identify the same process start. </summary>
    /// <param name="actualProcessStartedAtUtc"> The timestamp observed from the current or persisted process identity source. </param>
    /// <param name="expectedProcessStartedAtUtc"> The timestamp captured by the launcher or caller. </param>
    /// <returns> <see langword="true" /> when the timestamps are within <see cref="Tolerance" />; otherwise <see langword="false" />. </returns>
    public static bool Matches (
        DateTimeOffset actualProcessStartedAtUtc,
        DateTimeOffset expectedProcessStartedAtUtc)
    {
        var actualStartTimeUtc = actualProcessStartedAtUtc.ToUniversalTime();
        var expectedStartTimeUtc = expectedProcessStartedAtUtc.ToUniversalTime();
        var delta = actualStartTimeUtc - expectedStartTimeUtc;

        // The two process-start sources can be rounded to whole seconds in opposite directions.
        return delta.Duration() <= Tolerance;
    }
}
