using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Storage;

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
            StartupStatus: DaemonStartupStatusValues.Blocked,
            StartupBlockingReason: classification.StartupBlockingReason,
            LaunchAttemptId: null,
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ProcessAction: DaemonStartupProcessActionValues.Unknown,
            ProcessTermination: null,
            ArtifactPath: null,
            RetryDisposition: classification.RetryDisposition);

        return new StartupFailureDetail(
            startup,
            diagnosis,
            classification.RetryDisposition,
            string.Equals(classification.RetryDisposition, DaemonStartupRetryDispositionValues.RetryImmediately, StringComparison.Ordinal));
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
            StartupPhase: DaemonDiagnosisStartupPhaseValues.EndpointRegistration,
            ActionRequired: null,
            PrimaryDiagnostic: null);
        var startup = new DaemonStartupObservationOutput(
            StartupStatus: DaemonStartupStatusValues.Timeout,
            StartupBlockingReason: DaemonStartupBlockingReasonValues.EndpointNotRegistered,
            LaunchAttemptId: null,
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ProcessAction: DaemonStartupProcessActionValues.Unknown,
            ProcessTermination: null,
            ArtifactPath: null,
            RetryDisposition: DaemonStartupRetryDispositionValues.Unknown);

        return new StartupFailureDetail(
            startup,
            diagnosis,
            DaemonStartupRetryDispositionValues.Unknown,
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
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ProcessExit,
            ActionRequired: null,
            PrimaryDiagnostic: null);
        var startup = new DaemonStartupObservationOutput(
            StartupStatus: DaemonStartupStatusValues.Failed,
            StartupBlockingReason: DaemonStartupBlockingReasonValues.ProcessExit,
            LaunchAttemptId: null,
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ProcessAction: DaemonStartupProcessActionValues.Unknown,
            ProcessTermination: null,
            ArtifactPath: null,
            RetryDisposition: DaemonStartupRetryDispositionValues.Unknown);

        return new StartupFailureDetail(
            startup,
            diagnosis,
            DaemonStartupRetryDispositionValues.Unknown,
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
