using MackySoft.Ucli.Shared.Foundation;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Assesses whether one operating-system process still matches expected daemon session identity. </summary>
internal sealed class DaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
{
    private static readonly TimeSpan MaximumProcessStartLag = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan AllowedProcessStartLead = TimeSpan.FromSeconds(2);

    /// <summary> Resolves one process by identifier and assesses whether it matches expected daemon identity. </summary>
    /// <param name="processId"> The process identifier to assess. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    public DaemonProcessIdentityAssessment AssessByProcessId (
        int processId,
        DateTimeOffset expectedIssuedAtUtc)
    {
        ValidateAssessmentArguments(processId, expectedIssuedAtUtc);

        DiagnosticsProcess process;
        try
        {
            process = DiagnosticsProcess.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return new DaemonProcessIdentityAssessment(DaemonProcessIdentityAssessmentStatus.NotRunning, null, null);
        }

        using (process)
        {
            return AssessProcess(process, processId, expectedIssuedAtUtc);
        }
    }

    /// <summary> Assesses one already-resolved process against expected daemon identity. </summary>
    /// <param name="process"> The already-resolved process instance. </param>
    /// <param name="processId"> The process identifier used for diagnostics. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="process" /> is <see langword="null" />. </exception>
    public DaemonProcessIdentityAssessment AssessProcess (
        DiagnosticsProcess process,
        int processId,
        DateTimeOffset expectedIssuedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(process);
        ValidateAssessmentArguments(processId, expectedIssuedAtUtc);

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
                ExecutionError.InternalError(
                    $"Daemon process identity mismatch for process '{processId}'. " +
                    $"ExpectedStartRange=[{earliestAllowedStartTime:O}, {latestAllowedStartTime:O}] ActualStart={processStartTimeUtc:O}."));
        }

        return new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            processStartTimeUtc,
            null);
    }

    private static void ValidateAssessmentArguments (
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
    }

    private static bool HasExited (DiagnosticsProcess process)
    {
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