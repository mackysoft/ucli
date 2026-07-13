using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests;

internal static class DaemonStartCommandTestSupport
{
    public static DaemonStartExecutionOutput CreateSuccessOutput (
        IpcEditorLifecycleState lifecycleState = IpcEditorLifecycleState.Ready,
        IpcEditorBlockingReason? blockingReason = null,
        bool canAcceptExecutionRequests = true)
    {
        return new DaemonStartExecutionOutput(
            StartStatus: DaemonStartStatus.Started,
            DaemonStatus: DaemonStatusKind.Running,
            TimeoutMilliseconds: 1234,
            Session: new DaemonSessionOutput(
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
                IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 3, TimeSpan.Zero),
                EditorMode: DaemonEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: true,
                EndpointTransportKind: IpcTransportKind.NamedPipe,
                EndpointAddress: "ucli-daemon-endpoint",
                ProcessId: 1234,
                ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 0, TimeSpan.Zero),
                OwnerProcessId: 5678),
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            Generations: new IpcUnityGenerationSnapshot(0, 0, 0, 0),
            PlayMode: new IpcPlayModeSnapshot(
                IpcPlayModeState.Stopped,
                IpcPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false),
            CanAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    public static async ValueTask EmitSampleDaemonStartProgressAsync (
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        Assert.NotNull(progressSink);
        await progressSink!.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
                CreateProgressEntry(result: null, startStatus: null, daemonStatus: null, errorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed),
                CreateProgressEntry(CommandProgressResult.Succeeded, "started", "running", errorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask EmitUnknownStartedDaemonStartProgressAsync (
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        Assert.NotNull(progressSink);
        await progressSink!.OnEntryAsync(
                "daemon.start.future.started",
                CreateProgressEntry(result: null, startStatus: null, daemonStatus: null, errorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask EmitSampleSupervisorProgressAsync (
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        Assert.NotNull(progressSink);
        await progressSink!.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                DaemonStartProgressEntryTestFactory.CreateStartupObservation(
                    startedAtUtc: DaemonStartProgressEntryTestFactory.SampleStartedAtUtc,
                    startupStatus: DaemonStartupStatus.WaitingForEndpoint,
                    startupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.BlockerDetected),
                DaemonStartProgressEntryTestFactory.CreateStartupObservation(
                    startedAtUtc: DaemonStartProgressEntryTestFactory.SampleStartedAtUtc,
                    startupStatus: DaemonStartupStatus.Blocked,
                    startupBlockingReason: DaemonStartupBlockingReason.Compile,
                    startupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
                    retryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
                    message: "Unity startup is blocked.",
                    errorCode: DaemonErrorCodes.DaemonStartupBlocked.Value),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EndpointRegistered),
                DaemonStartProgressEntryTestFactory.CreateStartupObservation(
                    startedAtUtc: DaemonStartProgressEntryTestFactory.SampleStartedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved),
                new DaemonStartLifecycleSnapshotProgressEntry(
                    DaemonStartProgressPayloadKind.LifecycleSnapshot,
                    ProjectFingerprintTestFactory.Create("fingerprint"),
                    1234,
                    DaemonEditorMode.Batchmode,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    IpcEditorLifecycleState.Compiling,
                    IpcEditorBlockingReason.Compile,
                    new IpcUnityGenerationSnapshot(0, 0, 0, 0),
                    CanAcceptExecutionRequests: false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static DaemonDiagnosisOutput CreateDiagnosis (string reason)
    {
        return new DaemonDiagnosisOutput(
            Reason: reason,
            Message: "startup diagnosis",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: "/repo/UnityProject/Library/EditorInstance.json");
    }

    private static DaemonStartProgressEntry CreateProgressEntry (
        CommandProgressResult? result,
        string? startStatus,
        string? daemonStatus,
        string? errorCode)
    {
        return new DaemonStartProgressEntry(
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
            TimeoutMilliseconds: 1234,
            EditorMode: DaemonEditorMode.Batchmode,
            OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            Result: result,
            StartStatus: startStatus,
            DaemonStatus: daemonStatus,
            ErrorCode: errorCode);
    }
}
