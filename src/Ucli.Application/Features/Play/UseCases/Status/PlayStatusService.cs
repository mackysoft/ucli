using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Status;

/// <summary> Executes Play Mode status observation without launching a Unity Editor process. </summary>
internal sealed class PlayStatusService : IPlayStatusService
{
    private const string SessionNotAvailableMessage = "Registered GUI daemon session is not available for Play Mode status.";
    private const string RequiresGuiEditorMessage = "Play Mode status requires a registered GUI daemon session.";

    private readonly IPlayCommandExecutionContextResolver contextResolver;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlayStatusService" /> class. </summary>
    /// <param name="contextResolver"> The Play Mode command context resolver dependency. </param>
    /// <param name="unityRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlayStatusService (
        IPlayCommandExecutionContextResolver contextResolver,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<PlayStatusExecutionResult> ExecuteAsync (
        PlayStatusCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await contextResolver.ResolveAsync(
                input.ProjectPath,
                input.TimeoutMilliseconds,
                UcliCommandIds.PlayStatus,
                SessionNotAvailableMessage,
                RequiresGuiEditorMessage,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return PlayStatusExecutionResult.Failure(contextResult.Error!);
        }

        var playContext = contextResult.Context!;
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.PlayStatus,
                UnityExecutionMode.Daemon,
                playContext.Timeout,
                playContext.ProjectContext.Config,
                playContext.ProjectContext.UnityProject,
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
        if (!string.Equals(snapshot.ProjectFingerprint, playContext.Project.ProjectFingerprint, StringComparison.Ordinal))
        {
            return PlayStatusExecutionResult.Failure(ExecutionError.InternalError(
                $"Unity play status projectFingerprint mismatch. Requested={playContext.Project.ProjectFingerprint}, Actual={snapshot.ProjectFingerprint}."));
        }

        var lifecycle = LifecycleProjectionFactory.Create(snapshot);
        var guiEditorMode = DaemonEditorModeCodec.ToValue(DaemonEditorMode.Gui);
        if (!string.Equals(lifecycle.EditorMode, guiEditorMode, StringComparison.Ordinal))
        {
            return PlayStatusExecutionResult.Failure(CreateRequiresGuiEditorError());
        }

        if (lifecycle.PlayMode is null)
        {
            return PlayStatusExecutionResult.Failure(CreateStateUnknownError("Unity play status playMode snapshot is missing or invalid."));
        }

        var output = new PlayStatusExecutionOutput(
            Project: playContext.Project,
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: lifecycle.ServerVersion,
            EditorMode: guiEditorMode,
            LifecycleState: lifecycle.LifecycleState,
            BlockingReason: lifecycle.BlockingReason,
            CompileState: lifecycle.CompileState,
            CompileGeneration: lifecycle.CompileGeneration,
            DomainReloadGeneration: lifecycle.DomainReloadGeneration,
            CanAcceptExecutionRequests: lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: ToOutput(lifecycle.PrimaryDiagnostic),
            PlayMode: lifecycle.PlayMode,
            TimeoutMilliseconds: playContext.TimeoutMilliseconds);
        return PlayStatusExecutionResult.Success(output);
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
