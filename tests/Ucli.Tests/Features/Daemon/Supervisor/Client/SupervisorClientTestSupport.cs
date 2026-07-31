using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorClientTestSupport
{
    private static readonly ProjectFingerprint DefaultProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    public static SupervisorInstanceManifest CreateManifest (
        byte sessionTokenDiscriminator = 1,
        int? processId = null,
        SupervisorTransportEndpoint? endpoint = null,
        DateTimeOffset? issuedAtUtc = null)
    {
        return new SupervisorInstanceManifest(
            processId: processId ?? Environment.ProcessId,
            sessionToken: IpcSessionTokenTestFactory.CreateFromDiscriminator(sessionTokenDiscriminator),
            endpoint: endpoint ?? SupervisorTransportEndpoint.FromNamedPipeAddress("ucli-supervisor-test"),
            issuedAtUtc: issuedAtUtc ?? new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
    }

    public static SupervisorInstanceManifest CreateSuccessorManifest (
        SupervisorInstanceManifest current,
        byte sessionTokenDiscriminator)
    {
        ArgumentNullException.ThrowIfNull(current);
        return CreateManifest(
            sessionTokenDiscriminator,
            current.ProcessId,
            current.TransportEndpoint,
            current.IssuedAtUtc.AddSeconds(1));
    }

    public static ResolvedUnityProjectContext CreateUnityProject (ProjectFingerprint? projectFingerprint = null)
    {
        return ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: projectFingerprint ?? DefaultProjectFingerprint);
    }

    public static DaemonSession CreateGuiDaemonSession ()
    {
        return DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            editorMode: UnityEditorMode.Gui,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            processStartedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero),
            ownerProcessId: Environment.ProcessId);
    }

    public static UnityEditorObservation CreateReadyLifecycleObservation ()
    {
        return UnityEditorObservationTestFactory.Create(UnityEditorLifecycleState.Ready);
    }

    public static UnityEditorObservation CreateCompilingLifecycleObservation ()
    {
        return UnityEditorObservationTestFactory.Create(UnityEditorLifecycleState.Compiling);
    }

    public static DaemonStartupObservation CreateStartupObservation ()
    {
        return new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Blocked,
            StartupBlockingReason: DaemonStartupBlockingReason.Compile,
            LaunchAttemptId: null,
            ProcessAction: DaemonStartupProcessAction.Kept,
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            EditorMode: null,
            OwnerKind: null,
            CanShutdownProcess: null,
            ProcessId: null,
            StartedAtUtc: null,
            ElapsedMilliseconds: null,
            ArtifactPath: null);
    }

    public static IpcResponse CreateEnsureRunningResponse (
        IpcRequestEnvelope request,
        DaemonStartStatus startStatus = DaemonStartStatus.Started,
        DaemonSession? session = null,
        UnityEditorObservation? lifecycleObservation = null)
    {
        return new IpcResponse(
            protocolVersion: request.ProtocolVersion,
            requestId: request.RequestId,
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: startStatus,
                    Session: DaemonSessionContractMapper.ToContract(session ?? CreateGuiDaemonSession()),
                    LifecycleObservation: lifecycleObservation ?? CreateReadyLifecycleObservation())),
            errors: []);
    }

    public static IpcResponse CreateEnsureRunningFailureResponse (
        IpcRequestEnvelope request,
        DaemonDiagnosis diagnosis,
        DaemonStartupObservation startup,
        DaemonStatusKind daemonStatus = DaemonStatusKind.Stale)
    {
        return new IpcResponse(
            protocolVersion: request.ProtocolVersion,
            requestId: request.RequestId,
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(
                SupervisorEnsureRunningFailurePayloadMapper.ToContract(daemonStatus, diagnosis, startup)),
            errors:
            [
                new IpcError(ExecutionErrorCodes.IpcTimeout, "endpoint registration timed out", null),
            ]);
    }

    public static async ValueTask<IpcResponse> ForwardProgressThenReturnStartedAsync (
        IpcRequestEnvelope request,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        IpcStreamFrame progressFrame,
        CancellationToken cancellationToken)
    {
        await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
        return CreateEnsureRunningResponse(request);
    }

    public static IpcStreamFrame CreateProgressFrame<TPayload> (
        IpcRequestEnvelope request,
        string eventName,
        TPayload payload)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKind.Progress,
            eventName,
            IpcPayloadCodec.SerializeToElement(payload),
            response: null);
    }

    public static IpcStreamFrame CreateWaitingForEndpointProgressFrame (
        IpcRequestEnvelope request,
        DaemonStartupBlockedProcessPolicy onStartupBlocked = DaemonStartupBlockedProcessPolicy.Auto,
        string? message = null)
    {
        var progressPayload = DaemonStartProgressEntryTestFactory.CreateStartupObservation(
            timeoutMilliseconds: request.RequestDeadlineRemainingMilliseconds,
            editorMode: UnityEditorMode.Gui,
            onStartupBlocked: onStartupBlocked,
            processId: 42,
            startedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero),
            startupStatus: DaemonStartupStatus.WaitingForEndpoint,
            startupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            message: message);

        return CreateProgressFrame(
            request,
            TextVocabulary.GetText(DaemonStartProgressEvent.WaitingForEndpoint),
            progressPayload);
    }

    public static IpcStreamFrame CreateLifecycleSnapshotProgressFrame (
        IpcRequestEnvelope request,
        UnityEditorLifecycleState lifecycleState = UnityEditorLifecycleState.Compiling,
        UnityEditorBlockingReason? blockingReason = UnityEditorBlockingReason.Compile,
        bool canAcceptExecutionRequests = false)
    {
        var progressPayload = new DaemonStartLifecycleSnapshotProgressEntry(
            DaemonStartProgressPayloadKind.LifecycleSnapshot,
            DefaultProjectFingerprint,
            request.RequestDeadlineRemainingMilliseconds,
            UnityEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            lifecycleState,
            blockingReason,
            new UnityEditorGenerationSnapshot(0, 0, 0, 0),
            canAcceptExecutionRequests);

        return CreateProgressFrame(
            request,
            TextVocabulary.GetText(DaemonStartProgressEvent.LifecycleObserved),
            progressPayload);
    }
}
