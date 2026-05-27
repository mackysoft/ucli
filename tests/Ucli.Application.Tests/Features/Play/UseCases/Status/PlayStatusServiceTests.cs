using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayStatusServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProjectResolutionFails_ReturnsFailureWithoutSessionOrIpcCall ()
    {
        var expectedError = ExecutionError.InvalidArgument("Project resolution failed.");
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(ProjectContextResolutionResult.Failure(expectedError), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput("/missing/project", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
        Assert.Equal(0, sessionStore.ReadCallCount);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionIsMissing_ReturnsSessionNotAvailableWithoutIpcCall ()
    {
        var context = CreateContext();
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(null));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(PlayModeErrorCodes.PlayModeSessionNotAvailable, error.Code);
        Assert.Equal(0, requestExecutor.CallCount);
        Assert.Equal(context.UnityProject.RepositoryRoot, sessionStore.CapturedStorageRoot);
        Assert.Equal(context.UnityProject.ProjectFingerprint, sessionStore.CapturedProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRegisteredSessionIsBatchmode_ReturnsRequiresGuiEditorWithoutIpcCall ()
    {
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("batchmode")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(PlayModeErrorCodes.PlayModeRequiresGuiEditor, error.Code);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenGuiSessionAndIpcSucceeds_ReturnsFlatStatusProjection ()
    {
        var context = CreateContext();
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput("/repo/UnityProject", 1500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(context.UnityProject.UnityProjectRoot, output.Project.ProjectPath);
        Assert.Equal(context.UnityProject.ProjectFingerprint, output.Project.ProjectFingerprint);
        Assert.Equal("6000.1.4f1", output.Project.UnityVersion);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("gui", output.EditorMode);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.Equal(IpcCompileStateCodec.Ready, output.CompileState);
        Assert.Equal("12", output.CompileGeneration);
        Assert.Equal("7", output.DomainReloadGeneration);
        Assert.True(output.CanAcceptExecutionRequests);
        Assert.Equal("2026-05-21T00:00:00.0000000+00:00", output.ObservedAtUtc?.ToString("O", CultureInfo.InvariantCulture));
        Assert.Null(output.ActionRequired);
        Assert.Null(output.PrimaryDiagnostic);
        Assert.Equal("stopped", output.PlayMode.State);
        Assert.Equal("none", output.PlayMode.Transition);
        Assert.False(output.PlayMode.IsPlaying);
        Assert.False(output.PlayMode.IsPlayingOrWillChangePlaymode);
        Assert.Equal("2", output.PlayMode.Generation);
        Assert.Equal(1500, output.TimeoutMilliseconds);

        Assert.Equal(1, requestExecutor.CallCount);
        Assert.Equal(UcliCommandIds.PlayStatus, requestExecutor.CapturedCommand);
        Assert.Equal(UnityExecutionMode.Daemon, requestExecutor.CapturedMode);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), requestExecutor.CapturedTimeout);
        Assert.IsType<UnityRequestPayload.PlayStatus>(requestExecutor.CapturedPayload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlayModeIsPlaying_ReturnsPlayingSnapshot ()
    {
        var playMode = new IpcPlayModeSnapshot(
            State: "playing",
            Transition: "none",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "9");
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse(playMode: playMode))));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal("playing", output.PlayMode.State);
        Assert.Equal("none", output.PlayMode.Transition);
        Assert.True(output.PlayMode.IsPlaying);
        Assert.True(output.PlayMode.IsPlayingOrWillChangePlaymode);
        Assert.Equal("9", output.PlayMode.Generation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOut_ReturnsTimeoutError ()
    {
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            ExecutionErrorCodes.IpcTimeout,
            "play status timed out")));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOutAndFreshLifecycleSidecarExists_ReturnsSidecarStatus ()
    {
        var session = CreateSession("gui");
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var lifecycleStore = new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(
            session,
            IpcEditorLifecycleStateCodec.Ready,
            blockingReason: null,
            canAcceptExecutionRequests: true,
            playModeState: "stopped",
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false)));
        var processIdentityAssessor = new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            ExecutionErrorCodes.IpcTimeout,
            "play status timed out")));
        var service = CreateService(
            CreateContext(),
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.True(output.CanAcceptExecutionRequests);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("stopped", output.PlayMode.State);
        Assert.Equal("none", output.PlayMode.Transition);
        Assert.False(output.PlayMode.IsPlaying);
        Assert.Equal(1, lifecycleStore.ReadCallCount);
        Assert.Equal(1, processIdentityAssessor.CallCount);
        Assert.Equal(1, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOutAndLifecycleSidecarLacksEditorInstanceId_ReturnsTimeoutError ()
    {
        var session = CreateSession("gui");
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var lifecycleStore = new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(
            CreateLifecycleObservation(session) with
            {
                EditorInstanceId = null,
            }));
        var processIdentityAssessor = new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            ExecutionErrorCodes.IpcTimeout,
            "play status timed out")));
        var service = CreateService(
            CreateContext(),
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Equal(2, lifecycleStore.ReadCallCount);
        Assert.Equal(0, processIdentityAssessor.CallCount);
        Assert.Equal(1, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenFreshLifecycleSidecarReportsPlayMode_ReturnsWithoutIpcCall ()
    {
        var session = CreateSession("gui");
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var lifecycleStore = new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(session)));
        var processIdentityAssessor = new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(
            CreateContext(),
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal(IpcEditorLifecycleStateCodec.Playmode, output.LifecycleState);
        Assert.Equal("playing", output.PlayMode.State);
        Assert.Equal(0, requestExecutor.CallCount);
        Assert.Equal(1, lifecycleStore.ReadCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionFailsWithoutTimeout_PreservesFailureCodeAndMessage ()
    {
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
            "Daemon is not running.")));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, error.Code);
        Assert.Equal("Daemon is not running.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcErrorResponseIsReturned_PreservesErrorCode ()
    {
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            UcliCoreErrorCodes.InvalidArgument,
            "PlayStatus payload is invalid.")));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal("PlayStatus payload is invalid.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponseProjectFingerprintDiffers_ReturnsMismatchFailure ()
    {
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse(
            projectFingerprint: "other-project-fingerprint"))));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
        Assert.Contains("project-fingerprint", error.Message, StringComparison.Ordinal);
        Assert.Contains("other-project-fingerprint", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlayModeSnapshotIsInvalid_ReturnsStateUnknown ()
    {
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession("gui")));
        var statusResponse = CreateStatusResponse(playMode: new IpcPlayModeSnapshot(
            State: "invalid",
            Transition: "none",
            IsPlaying: false,
            IsPlayingOrWillChangePlaymode: false,
            Generation: "2"));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(statusResponse)));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionReadFails_ReturnsSessionReadErrorWithoutIpcCall ()
    {
        var expectedError = ExecutionError.InternalError("Failed to read daemon session.");
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Failure(expectedError));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    private static PlayStatusService CreateService (
        ProjectContext context,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor,
        IDaemonLifecycleStore? daemonLifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null)
    {
        return CreateService(
            ProjectContextResolutionResult.Success(context),
            sessionStore,
            requestExecutor,
            daemonLifecycleStore,
            processIdentityAssessor);
    }

    private static PlayStatusService CreateService (
        ProjectContextResolutionResult contextResult,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor,
        IDaemonLifecycleStore? daemonLifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null)
    {
        var contextResolver = new PlayCommandExecutionContextResolver(
            new StubProjectContextResolver(contextResult),
            sessionStore);
        return new PlayStatusService(
            contextResolver,
            requestExecutor,
            daemonLifecycleStore ?? new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(null)),
            processIdentityAssessor ?? new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.NotRunning));
    }

    private static ProjectContext CreateContext ()
    {
        var unityProjectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));
        return new ProjectContext(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: unityProjectRoot,
                RepositoryRoot: unityProjectRoot,
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption,
                UnityVersion: "6000.1.4f1"),
            Config: UcliConfig.CreateDefault(),
            ConfigSource: ConfigSource.Default);
    }

    private static DaemonSession CreateSession (string editorMode)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "project-fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: editorMode,
            OwnerKind: "user",
            CanShutdownProcess: false,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-play-status",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876)
        {
            EditorInstanceId = "editor-instance-1",
        };
    }

    private static DaemonLifecycleObservation CreateLifecycleObservation (
        DaemonSession session,
        string lifecycleState = IpcEditorLifecycleStateCodec.Playmode,
        string? blockingReason = IpcEditorBlockingReasonCodec.PlayMode,
        bool canAcceptExecutionRequests = false,
        string playModeState = "playing",
        bool isPlaying = true,
        bool isPlayingOrWillChangePlaymode = true)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: "gui",
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            ServerVersion = "0.5.0",
            CanAcceptExecutionRequests = canAcceptExecutionRequests,
            EditorInstanceId = session.EditorInstanceId,
            PlayMode = new IpcPlayModeSnapshot(
                State: playModeState,
                Transition: "none",
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode,
                Generation: "9"),
        };
    }

    private static IpcPlayStatusResponse CreateStatusResponse (
        IpcPlayModeSnapshot? playMode = null,
        string projectFingerprint = "project-fingerprint")
    {
        return new IpcPlayStatusResponse(new IpcPlayLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: projectFingerprint,
            LifecycleState: IpcEditorLifecycleStateCodec.Ready,
            BlockingReason: "none",
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode ?? new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "2")));
    }

    private static UnityRequestResponse CreateResponse (IpcPlayStatusResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: []));
    }

    private static UnityRequestResponse CreateErrorResponse (
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new IpcError(code, message, null),
            ]));
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        private readonly DaemonSessionReadResult readResult;

        public StubDaemonSessionStore (DaemonSessionReadResult readResult)
        {
            this.readResult = readResult;
        }

        public string? CapturedStorageRoot { get; private set; }

        public string? CapturedProjectFingerprint { get; private set; }

        public int ReadCallCount { get; private set; }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            CapturedStorageRoot = storageRoot;
            CapturedProjectFingerprint = projectFingerprint;
            return ValueTask.FromResult(readResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDaemonLifecycleStore : IDaemonLifecycleStore
    {
        private readonly DaemonLifecycleObservationReadResult readResult;

        public StubDaemonLifecycleStore (DaemonLifecycleObservationReadResult readResult)
        {
            this.readResult = readResult;
        }

        public int ReadCallCount { get; private set; }

        public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            return ValueTask.FromResult(readResult);
        }

        public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        private readonly DaemonProcessIdentityAssessmentStatus status;

        public StubDaemonProcessIdentityAssessor (DaemonProcessIdentityAssessmentStatus status)
        {
            this.status = status;
        }

        public int CallCount { get; private set; }

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset? expectedProcessStartedAtUtc)
        {
            CallCount++;
            return new DaemonProcessIdentityAssessment(
                status,
                ObservedStartTimeUtc: expectedProcessStartedAtUtc,
                Error: null);
        }
    }

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor
    {
        private readonly UnityRequestExecutionResult result;

        public StubUnityRequestExecutor (UnityRequestExecutionResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public UcliCommand CapturedCommand { get; private set; }

        public UnityExecutionMode CapturedMode { get; private set; }

        public TimeSpan CapturedTimeout { get; private set; }

        public UnityRequestPayload? CapturedPayload { get; private set; }

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedCommand = command;
            CapturedMode = mode;
            CapturedTimeout = timeout;
            CapturedPayload = payload;
            return ValueTask.FromResult(result);
        }
    }
}
