using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Shared.Foundation;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Identity;

/// <summary> Assesses whether one operating-system process still matches expected daemon session identity. </summary>
internal sealed class DaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
{
    /// <summary> Resolves one process by identifier and assesses whether it matches expected daemon identity. </summary>
    /// <param name="processId"> The process identifier to assess. </param>
    /// <param name="expectedProcessStartedAtUtc"> The expected process start timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    public DaemonProcessIdentityAssessment AssessByProcessId (
        int processId,
        DateTimeOffset? expectedProcessStartedAtUtc)
    {
        ValidateProcessId(processId);

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
            return AssessProcess(process, processId, expectedProcessStartedAtUtc);
        }
    }

    /// <summary> Assesses one already-resolved process against expected daemon identity. </summary>
    /// <param name="process"> The already-resolved process instance. </param>
    /// <param name="processId"> The process identifier used for diagnostics. </param>
    /// <param name="expectedProcessStartedAtUtc"> The expected process start timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="process" /> is <see langword="null" />. </exception>
    public DaemonProcessIdentityAssessment AssessProcess (
        DiagnosticsProcess process,
        int processId,
        DateTimeOffset? expectedProcessStartedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(process);
        ValidateProcessId(processId);

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

        if (expectedProcessStartedAtUtc is null || expectedProcessStartedAtUtc.Value == default)
        {
            return new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.Uncertain,
                processStartTimeUtc,
                ExecutionError.InternalError(
                    $"Daemon process identity could not be verified for process '{processId}' because expected processStartedAtUtc is not available."));
        }

        var expectedStartTimeUtc = expectedProcessStartedAtUtc.Value.ToUniversalTime();
        if (!DaemonProcessStartTimeMatcher.Matches(processStartTimeUtc, expectedStartTimeUtc))
        {
            return new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.DifferentProcess,
                processStartTimeUtc,
                ExecutionError.InternalError(
                    $"Daemon process identity mismatch for process '{processId}'. " +
                    $"ExpectedStart={expectedStartTimeUtc:O} ActualStart={processStartTimeUtc:O} Tolerance={DaemonProcessStartTimeMatcher.Tolerance}."));
        }

        return new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            processStartTimeUtc,
            null);
    }

    private static void ValidateProcessId (int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be greater than zero.");
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
