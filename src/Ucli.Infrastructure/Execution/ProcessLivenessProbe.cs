using System.ComponentModel;
using System.Diagnostics;

namespace MackySoft.Ucli.Infrastructure.Execution;

/// <summary> Probes whether one operating-system process identifier is still alive. </summary>
internal static class ProcessLivenessProbe
{
    private const long ProcessStartTimeToleranceTicks = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary> Gets whether the specified process identifier still points to a live process. </summary>
    /// <param name="processId"> The operating-system process identifier. </param>
    /// <returns> <see langword="true" /> when the process exists and has not exited; otherwise <see langword="false" />. </returns>
    internal static bool IsAlive (int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary> Gets whether an identifier still refers to the process generation started at the expected UTC timestamp. </summary>
    internal static bool IsSameProcess (
        int processId,
        DateTimeOffset expectedStartTimeUtc)
    {
        if (processId <= 0
            || expectedStartTimeUtc == default
            || expectedStartTimeUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return false;
            }

            return AreEquivalentProcessStartTimeMeasurements(
                expectedStartTimeUtc,
                new DateTimeOffset(process.StartTime.ToUniversalTime()));
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary> Compares process start times while tolerating sub-microsecond runtime conversion differences. </summary>
    internal static bool AreEquivalentProcessStartTimeMeasurements (
        DateTimeOffset expectedStartTimeUtc,
        DateTimeOffset actualStartTimeUtc)
    {
        var actualTicks = actualStartTimeUtc.UtcDateTime.Ticks;
        var expectedTicks = expectedStartTimeUtc.UtcDateTime.Ticks;
        var differenceTicks = actualTicks >= expectedTicks
            ? actualTicks - expectedTicks
            : expectedTicks - actualTicks;
        return differenceTicks <= ProcessStartTimeToleranceTicks;
    }
}
