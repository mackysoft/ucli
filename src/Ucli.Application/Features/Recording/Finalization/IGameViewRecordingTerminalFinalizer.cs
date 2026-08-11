using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Finalization;

/// <summary>Validates runtime termination and publishes the immutable recording terminal set.</summary>
internal interface IGameViewRecordingTerminalFinalizer
{
    ValueTask<GameViewRecordingTerminalFinalizationResult> FinalizeAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease artifactLease,
        GameViewRecordingStoredExecution stored,
        IpcGameViewRecordingTerminalSnapshot terminalSnapshot,
        Func<bool> canStartNextStage,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents the outcome of publishing a terminal recording set.</summary>
internal abstract record GameViewRecordingTerminalFinalizationResult
{
    public static GameViewRecordingTerminalFinalizationResult Success (
        GameViewRecordingTerminalPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new GameViewRecordingTerminalFinalizationSuccess(payload);
    }

    public static GameViewRecordingTerminalFinalizationResult Failure (
        GameViewRecordingRecoveryPayload recoveryPayload,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(recoveryPayload);
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingTerminalFinalizationFailure(recoveryPayload, error);
    }
}

/// <summary>Contains the fully published terminal payload.</summary>
internal sealed record GameViewRecordingTerminalFinalizationSuccess
    : GameViewRecordingTerminalFinalizationResult
{
    public GameViewRecordingTerminalFinalizationSuccess (
        GameViewRecordingTerminalPayload payload)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public GameViewRecordingTerminalPayload Payload { get; }
}

/// <summary>Contains the durable recovery checkpoint and terminal publication error.</summary>
internal sealed record GameViewRecordingTerminalFinalizationFailure
    : GameViewRecordingTerminalFinalizationResult
{
    public GameViewRecordingTerminalFinalizationFailure (
        GameViewRecordingRecoveryPayload recoveryPayload,
        ExecutionError error)
    {
        RecoveryPayload = recoveryPayload ?? throw new ArgumentNullException(nameof(recoveryPayload));
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public GameViewRecordingRecoveryPayload RecoveryPayload { get; }

    public ExecutionError Error { get; }
}
