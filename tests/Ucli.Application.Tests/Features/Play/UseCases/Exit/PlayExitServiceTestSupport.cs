using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Tests.Play;

internal static class PlayExitServiceTestSupport
{
    public const string PlaySessionEndpointAddress = "ucli-play-exit";

    public static readonly Guid ExecutionId =
        Guid.Parse("48a4577d-1460-46ab-b720-f56dc16860de");

    private static readonly DateTimeOffset StartedAtUtc =
        DateTimeOffset.Parse("2026-05-21T00:00:00Z", CultureInfo.InvariantCulture);

    public static readonly ProjectContext PlayProjectContext = ProjectContextTestFactory.CreateSingleRootProject();

    public static PlayExitService CreateService (
        ProjectContext context,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor,
        ILifecycleExecutionReconnectResolver? reconnectResolver = null,
        IGuidGenerator? executionIdGenerator = null,
        ILifecycleExecutionHostExitTerminalizer? hostExitTerminalizer = null,
        TimeProvider? timeProvider = null)
    {
        return CreateService(
            ProjectContextResolutionResult.Success(context),
            sessionStore,
            requestExecutor,
            reconnectResolver,
            executionIdGenerator,
            hostExitTerminalizer,
            timeProvider);
    }

    public static PlayExitService CreateService (
        ProjectContextResolutionResult contextResult,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor,
        ILifecycleExecutionReconnectResolver? reconnectResolver = null,
        IGuidGenerator? executionIdGenerator = null,
        ILifecycleExecutionHostExitTerminalizer? hostExitTerminalizer = null,
        TimeProvider? timeProvider = null)
    {
        var contextResolver = new PlayCommandExecutionContextResolver(
            new StaticProjectContextResolver(contextResult),
            sessionStore);
        var resolvedTimeProvider =
            timeProvider ?? new FakeTimeProvider(StartedAtUtc);
        return new PlayExitService(
            new PlayTransitionWorkflow(
                reconnectResolver
                    ?? new UnexpectedLifecycleExecutionReconnectResolver(),
                hostExitTerminalizer
                    ?? new UnexpectedLifecycleExecutionHostExitTerminalizer(),
                new LifecycleExecutionRegistrationIssuer(
                    executionIdGenerator
                        ?? new StaticGuidGenerator(ExecutionId),
                    resolvedTimeProvider),
                resolvedTimeProvider));
    }

    public static RecordingDaemonSessionStore CreateGuiSessionStore ()
    {
        return new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(
            DaemonSessionTestFactory.CreateUserOwned(
                UnityEditorMode.Gui,
                PlaySessionEndpointAddress,
                DaemonSessionTestFactory.DefaultEditorInstanceId)));
    }

    public static async ValueTask<LifecycleExecutionStartInvocation>
        CreateStartInvocationAsync (
            RecordingUnityRequestExecutor requestExecutor,
            ProjectContext? context = null,
            TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(requestExecutor);
        var resolvedContext = context ?? PlayProjectContext;
        var deadline = ExecutionDeadline.Start(
            TimeSpan.FromMilliseconds(1500),
            timeProvider ?? new FakeTimeProvider(StartedAtUtc));
        var bindingResult = await requestExecutor.BindAsync(
                UnityExecutionMode.Daemon,
                resolvedContext.UnityProject,
                deadline)
            .ConfigureAwait(false);
        return new LifecycleExecutionStartInvocation(
            new LifecycleExecutionFixedContext(
                resolvedContext,
                UnityExecutionMode.Daemon,
                bindingResult.Binding!),
            deadline,
            deadline.CreateCompletionDeadline(
                LifecycleExecutionTiming.ResponseDeliveryGrace),
            NullLifecycleExecutionStartObserver.Instance);
    }

    public static async ValueTask<LifecycleExecutionReconnectInvocation>
        CreateReconnectInvocationAsync (
            RecordingUnityRequestExecutor requestExecutor,
            ExecutionRef executionReference,
            ProjectContext? context = null,
            TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(requestExecutor);
        ArgumentNullException.ThrowIfNull(executionReference);
        var resolvedContext = context ?? PlayProjectContext;
        var deadline = ExecutionDeadline.Start(
            TimeSpan.FromMilliseconds(4500),
            timeProvider ?? new FakeTimeProvider(StartedAtUtc));
        var bindingResult = await requestExecutor.BindAsync(
                UnityExecutionMode.Daemon,
                resolvedContext.UnityProject,
                deadline)
            .ConfigureAwait(false);
        return new LifecycleExecutionReconnectInvocation(
            new LifecycleExecutionFixedContext(
                resolvedContext,
                UnityExecutionMode.Daemon,
                bindingResult.Binding!),
            executionReference,
            deadline);
    }

    public static IpcPlayTransitionResponse CreateExitedResponse ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var after = CreateSnapshot(
            UnityEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 3);
        return new IpcPlayTransitionResponse(
            CreateTerminalReference(),
            new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Exit,
                PlayLifecycleTransitionOutcome.Exited,
                before,
                After: after,
                Observed: null,
                ApplicationState: null));
    }

    public static UnityEditorObservation CreateSnapshot (
        UnityEditorLifecycleState lifecycleState,
        UnityEditorPlayModeSnapshot playMode,
        long playModeGeneration,
        ProjectFingerprint? projectFingerprint = null,
        UnityEditorMode editorMode = UnityEditorMode.Gui)
    {
        return new UnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: projectFingerprint ?? PlayProjectContext.UnityProject.ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: editorMode,
                lifecycleState: lifecycleState,
                compileState: lifecycleState == UnityEditorLifecycleState.Compiling
                    ? UnityEditorCompileState.Compiling
                    : UnityEditorCompileState.Ready,
                generations: new UnityEditorGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 4,
                    PlayModeGeneration: playModeGeneration),
                playMode: playMode),
            observedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            actionRequired: null,
            primaryDiagnostic: null);
    }

    public static UnityEditorPlayModeSnapshot CreatePlayingPlayMode ()
    {
        return new UnityEditorPlayModeSnapshot(
            State: UnityEditorPlayModeState.Playing,
            Transition: UnityEditorPlayModeTransition.None,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true);
    }

    public static UnityEditorPlayModeSnapshot CreateStoppedPlayMode ()
    {
        return new UnityEditorPlayModeSnapshot(
            State: UnityEditorPlayModeState.Stopped,
            Transition: UnityEditorPlayModeTransition.None,
            IsPlaying: false,
            IsPlayingOrWillChangePlaymode: false);
    }

    public static UnityRequestResponse CreateResponse (IpcPlayTransitionResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: []));
    }

    public static UnityRequestResponse CreateResponse (PlayLifecycleTransitionResult result)
    {
        return CreateResponse(new IpcPlayTransitionResponse(
            CreateTerminalReference(),
            result));
    }

    public static UnityRequestResponse CreateErrorResponse (
        PlayLifecycleTransitionResult result,
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(
                new IpcPlayTransitionErrorResponse(
                    CreateFailedTerminalReference(),
                    result.ApplicationState
                        ?? throw new InvalidOperationException(
                            "Failed Play Mode exit test result must include application state."),
                    result)),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }

    public static UnityRequestResponse CreateRecoverableErrorResponse (
        PlayLifecycleTransitionResult result,
        UcliCode code,
        string message,
        ExecutionApplicationState applicationState)
    {
        var terminalReference = CreateTerminalReference();
        var recoveryReference = new RecoveryExecutionRef(
            terminalReference.Kind,
            terminalReference.Id,
            terminalReference.DefinitionDigest,
            new ExecutionState(
                TextVocabulary.GetText(
                    LifecycleExecutionState.Publishing)),
            new ExecutionStatusLocator(
                $".ucli/local/lifecycle-executions/play.exit/{ExecutionId:N}/execution.json"));
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(
                new IpcPlayTransitionErrorResponse(
                    recoveryReference,
                    applicationState,
                    result)),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }

    public static UnityRequestResponse CreateTerminalErrorResponse (
        PlayLifecycleTransitionResult result,
        UcliCode code,
        string message,
        ExecutionApplicationState applicationState)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(
                new IpcPlayTransitionErrorResponse(
                    CreateFailedTerminalReference(),
                    applicationState,
                    result)),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }

    public static TerminalExecutionRef CreateTerminalReference ()
    {
        return CreateTerminalReference(
            LifecycleExecutionState.Completed,
            ExecutionId);
    }

    public static TerminalExecutionRef CreateFailedTerminalReference ()
    {
        return CreateTerminalReference(
            LifecycleExecutionState.Failed,
            ExecutionId);
    }

    public static TerminalExecutionRef CreateTerminalReference (
        Guid executionId)
    {
        return CreateTerminalReference(
            LifecycleExecutionState.Completed,
            executionId);
    }

    public static LifecycleExecutionStartBinding CreateStartBinding ()
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayExit);
        var registrationGeneration =
            Guid.Parse("7647b72d-0d2a-4574-86d4-ef6e29fa60e9");
        return new LifecycleExecutionStartBinding(
            new ActiveExecutionRef(
                definition.ExecutionKind,
                ExecutionId,
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                new ExecutionState(
                    TextVocabulary.GetText(LifecycleExecutionState.Exiting)),
                new ExecutionStatusLocator(
                    $".ucli/local/lifecycle-executions/play.exit/{ExecutionId:N}/execution.json")),
            new UnityProjectIdentity(
                PlayProjectContext.UnityProject.UnityProjectRoot.Value,
                PlayProjectContext.UnityProject.ProjectFingerprint,
                PlayProjectContext.UnityProject.UnityVersion),
            new LifecycleExecutionHostRegistration(
                new ProcessIdentity(4202, 123456),
                Guid.Parse("7ba2889a-8faf-4aa7-bacb-71a069339106"),
                registrationGeneration,
                registrationGeneration),
            new UnityEditorGenerationSnapshot(
                CompileGeneration: 12,
                DomainReloadGeneration: 7,
                AssetRefreshGeneration: 4,
                PlayModeGeneration: 3),
            StartedAtUtc.AddSeconds(2),
            StartedAtUtc);
    }

    private static TerminalExecutionRef CreateTerminalReference (
        LifecycleExecutionState state,
        Guid executionId)
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayExit);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(state)),
            statusLocator: null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $"lifecycle-executions/{executionId:N}/terminal-record.json"),
                Sha256Digest.Parse(new string('b', 64)),
                sizeBytes: 512,
                StartedAtUtc.AddSeconds(1)));
    }

    public static UnityRequestResponse CreateErrorResponseWithoutTransitionPayload (
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(
                new IpcPlayTransitionErrorResponse(
                    CreateStartBinding().LifecycleExecutionRef,
                    ExecutionApplicationState.Indeterminate,
                    result: null)),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }
}
