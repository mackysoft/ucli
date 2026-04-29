using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
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
