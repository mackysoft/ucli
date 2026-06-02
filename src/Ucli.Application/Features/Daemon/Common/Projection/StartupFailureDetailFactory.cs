using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.Projection;

/// <summary> Creates command-facing startup failure details from daemon startup classification data. </summary>
internal static class StartupFailureDetailFactory
{
    /// <summary> Creates a startup-blocked detail from a classified Unity startup log. </summary>
    public static StartupFailureDetail CreateClassifiedBatchmodeFailure (
        DaemonStartupFailureClassification classification,
        string message,
        string? unityLogPath,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var diagnosis = new DaemonDiagnosisOutput(
            Reason: classification.Reason,
            Message: message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: processId,
            EditorInstancePath: null,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: classification.StartupPhase,
            ActionRequired: classification.ActionRequired,
            PrimaryDiagnostic: ToOutput(classification.PrimaryDiagnostic));
        var startup = new DaemonStartupObservationOutput(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
            StartupBlockingReason: classification.StartupBlockingReason,
            LaunchAttemptId: null,
            EditorMode: ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
            OwnerKind: ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
            CanShutdownProcess: true,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown),
            ProcessTermination: null,
            ArtifactPath: null,
            RetryDisposition: classification.RetryDisposition);

        return new StartupFailureDetail(
            startup,
            diagnosis,
            classification.RetryDisposition,
            string.Equals(classification.RetryDisposition, ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryImmediately), StringComparison.Ordinal));
    }

    /// <summary> Creates a detail for an endpoint that never became reachable before the command timeout. </summary>
    public static StartupFailureDetail CreateEndpointNotRegisteredFailure (
        string message,
        string? unityLogPath,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var diagnosis = new DaemonDiagnosisOutput(
            Reason: DaemonDiagnosisReasonValues.StartupFailed,
            Message: message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: processId,
            EditorInstancePath: null,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration),
            ActionRequired: null,
            PrimaryDiagnostic: null);
        var startup = new DaemonStartupObservationOutput(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout),
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.EndpointNotRegistered),
            LaunchAttemptId: null,
            EditorMode: ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
            OwnerKind: ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
            CanShutdownProcess: true,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown),
            ProcessTermination: null,
            ArtifactPath: null,
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown));

        return new StartupFailureDetail(
            startup,
            diagnosis,
            ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown),
            false);
    }

    /// <summary> Creates a detail for an unclassified Unity process exit before endpoint or test-runner startup completed. </summary>
    public static StartupFailureDetail CreateProcessExitedFailure (
        string message,
        string? unityLogPath,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var diagnosis = new DaemonDiagnosisOutput(
            Reason: DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
            Message: message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: processId,
            EditorInstancePath: null,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ProcessExit),
            ActionRequired: null,
            PrimaryDiagnostic: null);
        var startup = new DaemonStartupObservationOutput(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.ProcessExit),
            LaunchAttemptId: null,
            EditorMode: ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
            OwnerKind: ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
            CanShutdownProcess: true,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown),
            ProcessTermination: null,
            ArtifactPath: null,
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown));

        return new StartupFailureDetail(
            startup,
            diagnosis,
            ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown),
            false);
    }

    private static DaemonPrimaryDiagnosticOutput? ToOutput (DaemonPrimaryDiagnostic? diagnostic)
    {
        return diagnostic is null
            ? null
            : new DaemonPrimaryDiagnosticOutput(
                Kind: diagnostic.Kind,
                Code: diagnostic.Code,
                File: diagnostic.File,
                Line: diagnostic.Line,
                Column: diagnostic.Column,
                Message: diagnostic.Message);
    }
}
