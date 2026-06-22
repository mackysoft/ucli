using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;

/// <summary> Represents normalized payload values for one daemon-start failure. </summary>
/// <param name="DaemonStatus"> The daemon status after start processing. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon start workflow. </param>
/// <param name="Startup"> The startup observation attached to the failure when available. </param>
/// <param name="Diagnosis"> The diagnosis attached to the failure when available. </param>
/// <param name="RetryDisposition"> The final retry disposition projected to the CLI payload. </param>
/// <param name="SafeToRetryImmediately"> Whether the failed command can be retried immediately without external changes. </param>
internal sealed record DaemonStartFailureExecutionOutput (
    DaemonStatusKind DaemonStatus,
    int TimeoutMilliseconds,
    DaemonStartupObservationOutput? Startup,
    DaemonDiagnosisOutput? Diagnosis,
    string RetryDisposition,
    bool SafeToRetryImmediately)
{
    /// <summary> Creates one failure output with final-response retry disposition normalization applied. </summary>
    public static DaemonStartFailureExecutionOutput Create (
        DaemonStatusKind daemonStatus,
        int timeoutMilliseconds,
        DaemonStartupObservation? startup,
        DaemonDiagnosisOutput? diagnosis)
    {
        var retryDisposition = ResolveFinalRetryDisposition(startup);
        return new DaemonStartFailureExecutionOutput(
            daemonStatus,
            timeoutMilliseconds,
            ToOutput(startup, retryDisposition),
            diagnosis,
            retryDisposition,
            ContractLiteralCodec.Matches(retryDisposition, DaemonStartupRetryDisposition.RetryImmediately));
    }

    private static string ResolveFinalRetryDisposition (DaemonStartupObservation? startup)
    {
        if (startup is null)
        {
            return ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown);
        }

        return ContractLiteralCodec.Matches(startup.RetryDisposition, DaemonStartupRetryDisposition.WaitThenRetry)
            ? ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown)
            : startup.RetryDisposition;
    }

    private static DaemonStartupObservationOutput? ToOutput (
        DaemonStartupObservation? startup,
        string retryDisposition)
    {
        if (startup is null)
        {
            return null;
        }

        return new DaemonStartupObservationOutput(
            StartupStatus: startup.StartupStatus,
            StartupBlockingReason: startup.StartupBlockingReason,
            LaunchAttemptId: startup.LaunchAttemptId,
            EditorMode: startup.EditorMode,
            OwnerKind: startup.OwnerKind,
            CanShutdownProcess: startup.CanShutdownProcess,
            ProcessId: startup.ProcessId,
            StartedAtUtc: startup.StartedAtUtc,
            ElapsedMilliseconds: startup.ElapsedMilliseconds,
            ProcessAction: startup.ProcessAction,
            ProcessTermination: null,
            ArtifactPath: startup.ArtifactPath,
            RetryDisposition: retryDisposition);
    }
}
