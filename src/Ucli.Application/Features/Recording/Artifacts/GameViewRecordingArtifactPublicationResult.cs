using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Represents publication of one immutable recording artifact. </summary>
internal sealed record GameViewRecordingArtifactPublicationResult
{
    private GameViewRecordingArtifactPublicationResult (
        PathArtifactRef? artifact,
        ExecutionError? error)
    {
        Artifact = artifact;
        Error = error;
    }

    public PathArtifactRef? Artifact { get; }

    public ExecutionError? Error { get; }

    public bool IsSuccess => Artifact is not null;

    public static GameViewRecordingArtifactPublicationResult Success (PathArtifactRef artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new GameViewRecordingArtifactPublicationResult(artifact, error: null);
    }

    public static GameViewRecordingArtifactPublicationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingArtifactPublicationResult(artifact: null, error);
    }
}
