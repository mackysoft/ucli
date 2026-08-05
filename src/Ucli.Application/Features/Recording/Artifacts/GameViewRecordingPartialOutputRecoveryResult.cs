using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary>Reports whether provider output was recovered, was absent, or could not be inspected and published.</summary>
internal sealed record GameViewRecordingPartialOutputRecoveryResult
{
    private GameViewRecordingPartialOutputRecoveryResult (
        PathArtifactRef? artifact,
        bool isAbsent,
        ExecutionError? error)
    {
        Artifact = artifact;
        IsAbsent = isAbsent;
        Error = error;
    }

    public PathArtifactRef? Artifact { get; }

    public bool IsAbsent { get; }

    public ExecutionError? Error { get; }

    public bool IsSuccess => Artifact is not null || IsAbsent;

    public static GameViewRecordingPartialOutputRecoveryResult Published (
        PathArtifactRef artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new GameViewRecordingPartialOutputRecoveryResult(
            artifact,
            isAbsent: false,
            error: null);
    }

    public static GameViewRecordingPartialOutputRecoveryResult Absent () =>
        new(artifact: null, isAbsent: true, error: null);

    public static GameViewRecordingPartialOutputRecoveryResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingPartialOutputRecoveryResult(
            artifact: null,
            isAbsent: false,
            error);
    }
}
