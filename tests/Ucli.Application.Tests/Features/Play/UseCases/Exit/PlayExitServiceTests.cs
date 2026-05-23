using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionIsMissing_ReturnsSessionNotAvailableWithoutIpcCall ()
    {
        var context = CreateContext();
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(null));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateExitedResponse())));
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(PlayModeErrorCodes.PlayModeSessionNotAvailable, result.Error!.Code);
        Assert.Equal(0, requestExecutor.CallCount);
        Assert.Equal(context.UnityProject.RepositoryRoot, sessionStore.CapturedStorageRoot);
        Assert.Equal(context.UnityProject.ProjectFingerprint, sessionStore.CapturedProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRegisteredSessionIsBatchmode_ReturnsRequiresGuiEditorWithoutIpcCall ()
    {
        var sessionStore = new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession(DaemonEditorModeValues.Batchmode)));
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateExitedResponse())));
        var service = CreateService(CreateContext(), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(PlayModeErrorCodes.PlayModeRequiresGuiEditor, result.Error!.Code);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitSucceeds_ReturnsReadyStoppedPayloadAndTransition ()
    {
        var context = CreateContext();
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateExitedResponse())));
        var service = CreateService(context, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput("/repo/UnityProject", 1500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayExitExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(context.UnityProject.UnityProjectRoot, output.Project.ProjectPath);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal(DaemonEditorModeValues.Gui, output.EditorMode);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.True(output.CanAcceptExecutionRequests);
        Assert.Equal(IpcPlayModeStateNames.Stopped, output.PlayMode.State);
        Assert.Equal("3", output.PlayMode.Generation);
        Assert.Equal(1500, output.TimeoutMilliseconds);
        Assert.Equal(IpcPlayTransitionCommandNames.Exit, output.Transition.Transition);
        Assert.Equal(IpcPlayTransitionResultNames.Exited, output.Transition.Result);
        Assert.NotNull(output.Transition.Before);
        Assert.NotNull(output.Transition.After);
        Assert.Null(output.Transition.Observed);
        Assert.Null(output.Transition.ApplicationState);

        Assert.Equal(1, requestExecutor.CallCount);
        Assert.Equal(UcliCommandIds.PlayExit, requestExecutor.CapturedCommand);
        Assert.Equal(UnityExecutionMode.Daemon, requestExecutor.CapturedMode);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), requestExecutor.CapturedTimeout);
        var payload = Assert.IsType<UnityRequestPayload.PlayExit>(requestExecutor.CapturedPayload);
        Assert.Equal(1500, payload.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAlreadyStopped_ReturnsAlreadyExitedWithoutGenerationChange ()
    {
        var before = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Compiling,
            IpcEditorBlockingReasonCodec.Compile,
            false,
            CreateStoppedPlayMode("9"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.AlreadyExited,
            before)
        {
            After = before,
        });
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(CreateContext(), CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayExitExecutionOutput>(result.Output);
        Assert.Equal(IpcPlayTransitionResultNames.AlreadyExited, output.Transition.Result);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, output.LifecycleState);
        Assert.Equal("9", output.Transition.Before.PlayMode!.Generation);
        Assert.Equal("9", output.Transition.After!.PlayMode!.Generation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsTransitionTimeout_ReturnsFailureWithObservedPayload ()
    {
        var before = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, CreatePlayingPlayMode("2"));
        var observed = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, new IpcPlayModeSnapshot(
            State: IpcPlayModeStateNames.Exiting,
            Transition: IpcPlayModeTransitionNames.Exiting,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "2"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Timeout,
            before)
        {
            Observed = observed,
            ApplicationState = IpcPlayApplicationStateNames.Indeterminate,
        });
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            "Unity Play Mode exit timed out after 1500 milliseconds.")));
        var service = CreateService(CreateContext(), CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, 1500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionTimeout, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcPlayTransitionResultNames.Timeout, result.Output!.Transition.Result);
        Assert.Equal(IpcPlayApplicationStateNames.Indeterminate, result.Output.Transition.ApplicationState);
        Assert.Equal(observed.PlayMode!.State, result.Output.Transition.Observed!.PlayMode!.State);
        Assert.Null(result.Output.Transition.After);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsAppliedBlockedTransition_ReturnsFailureWithoutAfter ()
    {
        var before = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, CreatePlayingPlayMode("2"));
        var observed = CreateSnapshot(IpcEditorLifecycleStateCodec.SafeMode, IpcEditorBlockingReasonCodec.SafeMode, false, CreateStoppedPlayMode("3"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Blocked,
            before)
        {
            Observed = observed,
            ApplicationState = IpcPlayApplicationStateNames.Applied,
        });
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            "Unity Play Mode exit completed but readiness was blocked.")));
        var service = CreateService(CreateContext(), CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionBlocked, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcPlayApplicationStateNames.Applied, result.Output!.Transition.ApplicationState);
        Assert.Null(result.Output.Transition.After);
        Assert.Equal(IpcEditorLifecycleStateCodec.SafeMode, result.Output.LifecycleState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponseProjectFingerprintDiffers_ReturnsMismatchFailure ()
    {
        var before = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            CreatePlayingPlayMode("2"),
            projectFingerprint: "other-project-fingerprint");
        var after = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true,
            CreateStoppedPlayMode("3"),
            projectFingerprint: "other-project-fingerprint");
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(CreateContext(), CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("projectFingerprint mismatch", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitedDoesNotChangeGeneration_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, CreatePlayingPlayMode("2"));
        var after = CreateSnapshot(IpcEditorLifecycleStateCodec.Ready, null, true, CreateStoppedPlayMode("2"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(CreateContext(), CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitedAfterSnapshotIsStillPlaymode_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, CreatePlayingPlayMode("2"));
        var after = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            CreateStoppedPlayMode("3"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(CreateContext(), CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityErrorOmitsTransitionPayload_ReturnsOriginalError ()
    {
        var requestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponseWithoutTransitionPayload(
            UcliCoreErrorCodes.InvalidArgument,
            "Unity play exit payload is invalid.")));
        var service = CreateService(CreateContext(), CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal("Unity play exit payload is invalid.", result.Error.Message);
    }

    private static PlayExitService CreateService (
        ProjectContext context,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor)
    {
        return CreateService(ProjectContextResolutionResult.Success(context), sessionStore, requestExecutor);
    }

    private static PlayExitService CreateService (
        ProjectContextResolutionResult contextResult,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor)
    {
        var contextResolver = new PlayCommandExecutionContextResolver(
            new StubProjectContextResolver(contextResult),
            sessionStore);
        return new PlayExitService(contextResolver, requestExecutor);
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

    private static StubDaemonSessionStore CreateGuiSessionStore ()
    {
        return new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession(DaemonEditorModeValues.Gui)));
    }

    private static DaemonSession CreateSession (string editorMode)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "project-fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: editorMode,
            OwnerKind: DaemonSessionOwnerKindValues.User,
            CanShutdownProcess: false,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-play-exit",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private static IpcPlayTransitionResponse CreateExitedResponse ()
    {
        var before = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, CreatePlayingPlayMode("2"));
        var after = CreateSnapshot(IpcEditorLifecycleStateCodec.Ready, null, true, CreateStoppedPlayMode("3"));
        return new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
    }

    private static IpcPlayLifecycleSnapshot CreateSnapshot (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests,
        IpcPlayModeSnapshot playMode,
        string projectFingerprint = "project-fingerprint")
    {
        return new IpcPlayLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: DaemonEditorModeValues.Gui,
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: projectFingerprint,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode);
    }

    private static IpcPlayModeSnapshot CreatePlayingPlayMode (string generation)
    {
        return new IpcPlayModeSnapshot(
            State: IpcPlayModeStateNames.Playing,
            Transition: IpcPlayModeTransitionNames.None,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: generation);
    }

    private static IpcPlayModeSnapshot CreateStoppedPlayMode (string generation)
    {
        return new IpcPlayModeSnapshot(
            State: IpcPlayModeStateNames.Stopped,
            Transition: IpcPlayModeTransitionNames.None,
            IsPlaying: false,
            IsPlayingOrWillChangePlaymode: false,
            Generation: generation);
    }

    private static UnityRequestResponse CreateResponse (IpcPlayTransitionResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: []));
    }

    private static UnityRequestResponse CreateErrorResponse (
        IpcPlayTransitionResponse payload,
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors:
            [
                new IpcError(code, message, null),
            ]));
    }

    private static UnityRequestResponse CreateErrorResponseWithoutTransitionPayload (
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new
            {
                ignored = true,
            }),
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

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor
    {
        private readonly Queue<UnityRequestExecutionResult> results;

        public StubUnityRequestExecutor (params UnityRequestExecutionResult[] results)
        {
            if (results.Length == 0)
            {
                throw new ArgumentException("At least one result is required.", nameof(results));
            }

            this.results = new Queue<UnityRequestExecutionResult>(results);
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
            var result = results.Count == 1 ? results.Peek() : results.Dequeue();
            return ValueTask.FromResult(result);
        }
    }
}
