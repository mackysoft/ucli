using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Represents opening an existing recording-scoped artifact lease. </summary>
internal sealed record GameViewRecordingArtifactOpenResult
{
    private GameViewRecordingArtifactOpenResult (
        IGameViewRecordingArtifactLease? lease,
        ExecutionError? error)
    {
        Lease = lease;
        Error = error;
    }

    public IGameViewRecordingArtifactLease? Lease { get; }

    public ExecutionError? Error { get; }

    public bool IsSuccess => Lease is not null;

    public static GameViewRecordingArtifactOpenResult Success (
        IGameViewRecordingArtifactLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new GameViewRecordingArtifactOpenResult(lease, error: null);
    }

    public static GameViewRecordingArtifactOpenResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingArtifactOpenResult(lease: null, error);
    }
}
