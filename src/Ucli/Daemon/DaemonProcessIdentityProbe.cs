using System.Diagnostics;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Probes whether one operating-system process still matches expected daemon session identity. </summary>
internal static class DaemonProcessIdentityProbe
{
    private static readonly TimeSpan MaximumProcessStartLag = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan AllowedProcessStartLead = TimeSpan.FromSeconds(2);

    /// <summary> Assesses whether the specified process identifier still matches the expected daemon identity. </summary>
    /// <param name="processId"> The process identifier to assess. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    public static DaemonProcessIdentityAssessment Assess (
        int processId,
        DateTimeOffset expectedIssuedAtUtc)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be greater than zero.");
        }

        if (expectedIssuedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedIssuedAtUtc), expectedIssuedAtUtc, "Expected issuedAtUtc must not be default.");
        }

        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return new DaemonProcessIdentityAssessment(DaemonProcessIdentityAssessmentStatus.NotRunning, null, null);
        }

        using (process)
        {
            if (HasExited(process))
            {
                return new DaemonProcessIdentityAssessment(DaemonProcessIdentityAssessmentStatus.NotRunning, null, null);
            }

            DateTimeOffset processStartTimeUtc;
            try
            {
                processStartTimeUtc = process.StartTime.ToUniversalTime();
            }
            catch (InvalidOperationException) when (HasExited(process))
            {
                return new DaemonProcessIdentityAssessment(DaemonProcessIdentityAssessmentStatus.NotRunning, null, null);
            }
            catch (Exception exception)
            {
                return new DaemonProcessIdentityAssessment(
                    DaemonProcessIdentityAssessmentStatus.Uncertain,
                    null,
                    ExecutionError.InternalError(
                        $"Failed to validate daemon process identity for process '{processId}'. {exception.Message}"));
            }

            var earliestAllowedStartTime = expectedIssuedAtUtc - AllowedProcessStartLead;
            var latestAllowedStartTime = expectedIssuedAtUtc + MaximumProcessStartLag;
            if (processStartTimeUtc < earliestAllowedStartTime || processStartTimeUtc > latestAllowedStartTime)
            {
                return new DaemonProcessIdentityAssessment(
                    DaemonProcessIdentityAssessmentStatus.DifferentProcess,
                    processStartTimeUtc,
                    null);
            }

            return new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                processStartTimeUtc,
                null);
        }
    }

    /// <summary> Gets the earliest allowed process start time for one expected issued-at timestamp. </summary>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The earliest allowed process start time. </returns>
    public static DateTimeOffset GetEarliestAllowedStartTime (DateTimeOffset expectedIssuedAtUtc)
    {
        if (expectedIssuedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedIssuedAtUtc), expectedIssuedAtUtc, "Expected issuedAtUtc must not be default.");
        }

        return expectedIssuedAtUtc - AllowedProcessStartLead;
    }

    /// <summary> Gets the latest allowed process start time for one expected issued-at timestamp. </summary>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The latest allowed process start time. </returns>
    public static DateTimeOffset GetLatestAllowedStartTime (DateTimeOffset expectedIssuedAtUtc)
    {
        if (expectedIssuedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedIssuedAtUtc), expectedIssuedAtUtc, "Expected issuedAtUtc must not be default.");
        }

        return expectedIssuedAtUtc + MaximumProcessStartLag;
    }

    /// <summary> Gets whether target process is already exited while tolerating post-exit access races. </summary>
    /// <param name="process"> The target process instance. </param>
    /// <returns> <see langword="true" /> when process already exited; otherwise <see langword="false" />. </returns>
    public static bool HasExited (Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}