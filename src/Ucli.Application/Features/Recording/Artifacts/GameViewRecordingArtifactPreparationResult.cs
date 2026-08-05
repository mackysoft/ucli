using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Represents creation of one recording-scoped artifact lease. </summary>
internal sealed record GameViewRecordingArtifactPreparationResult
{
    private GameViewRecordingArtifactPreparationResult (
        IGameViewRecordingArtifactLease? lease,
        ExecutionError? error)
    {
        Lease = lease;
        Error = error;
    }

    public IGameViewRecordingArtifactLease? Lease { get; }

    public ExecutionError? Error { get; }

    public bool IsSuccess => Lease is not null;

    public static GameViewRecordingArtifactPreparationResult Success (
        IGameViewRecordingArtifactLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new GameViewRecordingArtifactPreparationResult(lease, error: null);
    }

    public static GameViewRecordingArtifactPreparationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingArtifactPreparationResult(lease: null, error);
    }
}
