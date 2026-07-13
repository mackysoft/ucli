using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorClientTestSupport
{
    public static SupervisorInstanceManifest CreateManifest (
        int? processId = null,
        string endpointTransportKind = "namedPipe")
    {
        return new SupervisorInstanceManifest(
            ProcessId: processId ?? Environment.ProcessId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: endpointTransportKind,
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
    }

    public static ResolvedUnityProjectContext CreateUnityProject (string projectFingerprint = "fingerprint")
    {
        return ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: projectFingerprint);
    }

    public static DaemonSession CreateGuiDaemonSession ()
    {
        return DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            editorMode: DaemonEditorMode.Gui,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            processStartedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero),
            ownerProcessId: Environment.ProcessId);
    }

    public static IpcUnityEditorObservation CreateReadyLifecycleObservation ()
    {
        return IpcUnityEditorObservationTestFactory.Create(IpcEditorLifecycleState.Ready);
    }

    public static IpcUnityEditorObservation CreateCompilingLifecycleObservation ()
    {
        return IpcUnityEditorObservationTestFactory.Create(IpcEditorLifecycleState.Compiling);
    }

    public static DaemonStartupObservation CreateStartupObservation ()
    {
        return new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Blocked,
            StartupBlockingReason: DaemonStartupBlockingReason.Compile,
            LaunchAttemptId: null,
            ProcessAction: DaemonStartupProcessAction.Kept,
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix);
    }

    public static IpcResponse CreateEnsureRunningResponse (
        IpcRequest request,
        string startStatus = "started",
        string daemonStatus = "running",
        DaemonSession? session = null,
        IpcUnityEditorObservation? lifecycleObservation = null)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: startStatus,
                    DaemonStatus: daemonStatus,
                    Session: session ?? CreateGuiDaemonSession(),
                    LifecycleObservation: lifecycleObservation ?? CreateReadyLifecycleObservation())),
            Errors: []);
    }

    public static IpcResponse CreateEnsureRunningFailureResponse (
        IpcRequest request,
        DaemonDiagnosis diagnosis,
        DaemonStartupObservation startup,
        string daemonStatus = "stale")
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.EnsureRunningFailureResponse(daemonStatus, diagnosis, startup)),
            Errors:
            [
                new IpcError(ExecutionErrorCodes.IpcTimeout, "endpoint registration timed out", null),
            ]);
    }

    public static async ValueTask<IpcResponse> ForwardProgressThenReturnStartedAsync (
        IpcRequest request,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        IpcStreamFrame progressFrame,
        CancellationToken cancellationToken)
    {
        await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
        return CreateEnsureRunningResponse(request);
    }

    public static IpcStreamFrame CreateProgressFrame<TPayload> (
        IpcRequest request,
        string eventName,
        TPayload payload)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Progress,
            eventName,
            IpcPayloadCodec.SerializeToElement(payload),
            Response: null);
    }

    public static IpcStreamFrame CreateWaitingForEndpointProgressFrame (
        IpcRequest request,
        DaemonStartupBlockedProcessPolicy onStartupBlocked = DaemonStartupBlockedProcessPolicy.Auto,
        string? message = null)
    {
        var progressPayload = DaemonStartProgressEntryTestFactory.CreateStartupObservation(
            timeoutMilliseconds: 5000,
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: onStartupBlocked,
            processId: 42,
            startedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero),
            startupStatus: DaemonStartupStatus.WaitingForEndpoint,
            startupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            message: message);

        return CreateProgressFrame(
            request,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
            progressPayload);
    }

    public static IpcStreamFrame CreateLifecycleSnapshotProgressFrame (
        IpcRequest request,
        IpcEditorLifecycleState lifecycleState = IpcEditorLifecycleState.Compiling,
        IpcEditorBlockingReason? blockingReason = IpcEditorBlockingReason.Compile,
        bool canAcceptExecutionRequests = false)
    {
        var progressPayload = new DaemonStartLifecycleSnapshotProgressEntry(
            DaemonStartProgressPayloadKind.LifecycleSnapshot,
            "fingerprint",
            5000,
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            lifecycleState,
            blockingReason,
            new IpcUnityGenerationSnapshot(0, 0, 0, 0),
            canAcceptExecutionRequests);

        return CreateProgressFrame(
            request,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved),
            progressPayload);
    }
}
