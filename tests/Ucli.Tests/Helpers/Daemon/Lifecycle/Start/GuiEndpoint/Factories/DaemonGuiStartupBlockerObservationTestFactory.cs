using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonGuiStartupBlockerObservationTestFactory
{
    public static DaemonGuiStartupBlockerObservation Create (
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        DaemonStartupBlockingReason startupBlockingReason = DaemonStartupBlockingReason.Compile,
        DaemonDiagnosisReason reason = DaemonDiagnosisReason.UnityScriptCompilationFailed,
        DaemonStartupRetryDisposition retryDisposition = DaemonStartupRetryDisposition.RetryAfterFix,
        string message = "Unity Editor startup is blocked because scripts have compiler errors.",
        DaemonDiagnosisStartupPhase startupPhase = DaemonDiagnosisStartupPhase.ScriptCompilation,
        DaemonDiagnosisActionRequired actionRequired = DaemonDiagnosisActionRequired.FixCompileErrors,
        DaemonPrimaryDiagnostic? primaryDiagnostic = null)
    {
        return new DaemonGuiStartupBlockerObservation(
            new DaemonStartupFailureClassification(
                startupBlockingReason,
                reason,
                retryDisposition,
                message,
                startupPhase,
                actionRequired,
                primaryDiagnostic),
            processId,
            processStartedAtUtc,
            unityLogPath);
    }
}
