using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.Common.Projection;
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

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="PlayStatusService" /> class. </summary>
    /// <param name="contextResolver"> The Play Mode command context resolver dependency. </param>
    /// <param name="unityRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlayStatusService (
        IPlayCommandExecutionContextResolver contextResolver,
        IUnityRequestExecutor unityRequestExecutor,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        TimeProvider? timeProvider = null)
    {
        this.contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
        var observedStatus = await TryCreateOutputFromLifecycleObservationAsync(
                playContext,
                cancellationToken)
            .ConfigureAwait(false);

        // NOTE: A fresh sidecar is written by Unity main-thread lifecycle callbacks. Once the editor is not
        // ready, waiting for a normal daemon IPC status response only delays the same authoritative observation.
        if (observedStatus is not null && observedStatus.LifecycleState != IpcEditorLifecycleState.Ready)
        {
            return PlayStatusExecutionResult.Success(observedStatus.Output);
        }

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
            if (executionResult.FailureInfo!.Code == ExecutionErrorCodes.IpcTimeout)
            {
                var fallbackStatus = observedStatus
                    ?? await TryCreateOutputFromLifecycleObservationAsync(
                            playContext,
                            cancellationToken)
                        .ConfigureAwait(false);
                if (fallbackStatus is not null)
                {
                    return PlayStatusExecutionResult.Success(fallbackStatus.Output);
                }
            }

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

        var lifecycle = PlayOutputProjectionFactory.CreateSnapshotOutput(snapshot);
        if (lifecycle.EditorMode != DaemonEditorMode.Gui)
        {
            return PlayStatusExecutionResult.Failure(CreateRequiresGuiEditorError());
        }

        var output = new PlayStatusExecutionOutput(
            Project: playContext.Project,
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: lifecycle.ServerVersion,
            EditorMode: DaemonEditorMode.Gui,
            LifecycleState: lifecycle.LifecycleState,
            BlockingReason: lifecycle.BlockingReason,
            CompileState: lifecycle.CompileState,
            Generations: lifecycle.Generations,
            CanAcceptExecutionRequests: lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: lifecycle.PrimaryDiagnostic,
            PlayMode: lifecycle.PlayMode,
            TimeoutMilliseconds: playContext.TimeoutMilliseconds);
        return PlayStatusExecutionResult.Success(output);
    }

    private async ValueTask<ObservedPlayStatus?> TryCreateOutputFromLifecycleObservationAsync (
        PlayCommandExecutionContext playContext,
        CancellationToken cancellationToken)
    {
        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                playContext.ProjectContext.UnityProject.RepositoryRoot,
                playContext.ProjectContext.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var observation = lifecycleReadResult.Observation;
        if (!lifecycleReadResult.IsSuccess
            || !lifecycleReadResult.Exists
            || observation is null
            || !DaemonLifecycleObservationAvailability.IsUsableForSession(
                observation,
                playContext.Session,
                processIdentityAssessor,
                timeProvider))
        {
            return null;
        }

        if (observation.State.EditorMode != DaemonEditorMode.Gui)
        {
            return null;
        }

        var output = new PlayStatusExecutionOutput(
            Project: playContext.Project,
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: observation.ServerVersion,
            EditorMode: DaemonEditorMode.Gui,
            LifecycleState: observation.State.LifecycleState,
            BlockingReason: observation.BlockingReason,
            CompileState: observation.State.CompileState,
            Generations: observation.State.Generations,
            CanAcceptExecutionRequests: observation.CanAcceptExecutionRequests,
            ObservedAtUtc: observation.ObservedAtUtc,
            ActionRequired: observation.ActionRequired,
            PrimaryDiagnostic: ToOutput(observation.PrimaryDiagnostic),
            PlayMode: observation.State.PlayMode,
            TimeoutMilliseconds: playContext.TimeoutMilliseconds);
        return new ObservedPlayStatus(output, observation.State.LifecycleState);
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

    /// <summary> Couples one CLI output projection with the typed lifecycle state used by pre-IPC policy. </summary>
    private sealed record ObservedPlayStatus (
        PlayStatusExecutionOutput Output,
        IpcEditorLifecycleState LifecycleState);
}
