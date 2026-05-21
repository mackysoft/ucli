using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Status;

/// <summary> Executes Play Mode status observation without launching a Unity Editor process. </summary>
internal sealed class PlayStatusService : IPlayStatusService
{
    private const string SessionNotAvailableMessage = "Registered GUI daemon session is not available for Play Mode status.";
    private const string RequiresGuiEditorMessage = "Play Mode status requires a registered GUI daemon session.";

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlayStatusService" /> class. </summary>
    /// <param name="projectContextResolver"> The project-context resolver dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="unityRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlayStatusService (
        IProjectContextResolver projectContextResolver,
        IDaemonSessionStore daemonSessionStore,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<PlayStatusExecutionResult> ExecuteAsync (
        PlayStatusCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return PlayStatusExecutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.PlayStatus,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return PlayStatusExecutionResult.Failure(timeoutResult.Error!);
        }

        var timeout = timeoutResult.Timeout!.Value;
        var sessionResult = await daemonSessionStore.ReadAsync(
                context.UnityProject.RepositoryRoot,
                context.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sessionResult.IsSuccess)
        {
            return PlayStatusExecutionResult.Failure(sessionResult.Error!);
        }

        if (!sessionResult.Exists)
        {
            return PlayStatusExecutionResult.Failure(CreateSessionNotAvailableError());
        }

        if (!IsGuiSession(sessionResult.Session!))
        {
            return PlayStatusExecutionResult.Failure(CreateRequiresGuiEditorError());
        }

        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.PlayStatus,
                UnityExecutionMode.Daemon,
                timeout,
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.PlayStatus(),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return PlayStatusExecutionResult.Failure(CreateErrorFromUnityRequestFailure(executionResult.FailureInfo!));
        }

        var response = executionResult.Response!;
        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            return PlayStatusExecutionResult.Failure(CreateErrorFromResponse(response));
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayStatusResponse statusResponse, out var payloadError))
        {
            return PlayStatusExecutionResult.Failure(ExecutionError.InternalError(
                $"Unity play status payload is invalid. {payloadError.Message}"));
        }

        if (statusResponse.Snapshot is null)
        {
            return PlayStatusExecutionResult.Failure(CreateStateUnknownError("Unity play status snapshot is missing."));
        }

        var snapshot = statusResponse.Snapshot;
        if (!IsGuiEditorMode(snapshot.EditorMode, out var editorMode))
        {
            return PlayStatusExecutionResult.Failure(CreateRequiresGuiEditorError());
        }

        var playMode = PlayModeSnapshotOutputFactory.Create(snapshot.PlayMode);
        if (playMode is null)
        {
            return PlayStatusExecutionResult.Failure(CreateStateUnknownError("Unity play status playMode snapshot is missing or invalid."));
        }

        var lifecycle = CreateLifecycleProjection(snapshot);
        var output = new PlayStatusExecutionOutput(
            Project: project,
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: lifecycle.ServerVersion,
            EditorMode: editorMode,
            LifecycleState: lifecycle.LifecycleState,
            BlockingReason: lifecycle.BlockingReason,
            CompileState: lifecycle.CompileState,
            CompileGeneration: lifecycle.CompileGeneration,
            DomainReloadGeneration: lifecycle.DomainReloadGeneration,
            CanAcceptExecutionRequests: lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: ToOutput(lifecycle.PrimaryDiagnostic),
            PlayMode: playMode,
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds));
        return PlayStatusExecutionResult.Success(output);
    }

    private static bool IsGuiSession (DaemonSession session)
    {
        return IsGuiEditorMode(session.EditorMode, out _);
    }

    private static bool IsGuiEditorMode (
        string? value,
        out string editorMode)
    {
        if (DaemonEditorModeCodec.TryParse(value, out var parsedMode)
            && parsedMode == DaemonEditorMode.Gui)
        {
            editorMode = DaemonEditorModeCodec.ToValue(parsedMode);
            return true;
        }

        editorMode = string.Empty;
        return false;
    }

    private static PingLifecycleProjection CreateLifecycleProjection (IpcPlayLifecycleSnapshot snapshot)
    {
        var lifecycleState = IpcEditorLifecycleStateCodec.TryParse(snapshot.LifecycleState, out var normalizedLifecycleState)
            ? normalizedLifecycleState
            : null;
        var blockingReason = lifecycleState is null || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
            ? null
            : IpcEditorBlockingReasonCodec.TryParse(snapshot.BlockingReason, out var normalizedBlockingReason)
                ? normalizedBlockingReason
                : null;
        var canAcceptExecutionRequests = string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
            && snapshot.CanAcceptExecutionRequests;

        return new PingLifecycleProjection(
            ServerVersion: StringValueNormalizer.TrimToNull(snapshot.ServerVersion),
            UnityVersion: StringValueNormalizer.TrimToNull(snapshot.UnityVersion),
            EditorMode: DaemonEditorModeCodec.TryParse(snapshot.EditorMode, out var editorMode)
                ? DaemonEditorModeCodec.ToValue(editorMode)
                : null,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: IpcCompileStateCodec.TryParse(snapshot.CompileState, out var compileState)
                ? compileState
                : null,
            CompileGeneration: StringValueNormalizer.TrimToNull(snapshot.CompileGeneration),
            DomainReloadGeneration: StringValueNormalizer.TrimToNull(snapshot.DomainReloadGeneration),
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: snapshot.ObservedAtUtc,
            ActionRequired: StringValueNormalizer.TrimToNull(snapshot.ActionRequired),
            PrimaryDiagnostic: snapshot.PrimaryDiagnostic,
            PlayMode: PlayModeSnapshotOutputFactory.Create(snapshot.PlayMode));
    }

    private static DaemonPrimaryDiagnosticOutput? ToOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !StringValueNormalizer.TryTrimToNonEmpty(diagnostic.Kind, out var kind))
        {
            return null;
        }

        return new DaemonPrimaryDiagnosticOutput(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(diagnostic.Code),
            File: StringValueNormalizer.TrimToNull(diagnostic.File),
            Line: diagnostic.Line,
            Column: diagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(diagnostic.Message));
    }

    private static ExecutionError CreateErrorFromUnityRequestFailure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Code == ExecutionErrorCodes.IpcTimeout
            ? ExecutionError.Timeout(failure.Message, failure.Code)
            : ExecutionError.InternalError(failure.Message, failure.Code);
    }

    private static ExecutionError CreateErrorFromResponse (UnityRequestResponse response)
    {
        var firstError = response.Errors.FirstOrDefault();
        var message = string.IsNullOrWhiteSpace(firstError?.Message)
            ? $"Unity play status IPC failed with status '{response.FailureStatus}'."
            : firstError!.Message;
        var code = firstError?.Code;
        return code == ExecutionErrorCodes.IpcTimeout
            ? ExecutionError.Timeout(message, code)
            : ExecutionError.InternalError(message, code);
    }

    private static ExecutionError CreateSessionNotAvailableError ()
    {
        return ExecutionError.InternalError(
            SessionNotAvailableMessage,
            PlayModeErrorCodes.PlayModeSessionNotAvailable);
    }

    private static ExecutionError CreateRequiresGuiEditorError ()
    {
        return ExecutionError.InternalError(
            RequiresGuiEditorMessage,
            PlayModeErrorCodes.PlayModeRequiresGuiEditor);
    }

    private static ExecutionError CreateStateUnknownError (string message)
    {
        return ExecutionError.InternalError(message, PlayModeErrorCodes.PlayModeStateUnknown);
    }
}
