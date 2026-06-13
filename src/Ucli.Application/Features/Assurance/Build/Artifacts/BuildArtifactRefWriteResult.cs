using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the result of writing one artifact reference. </summary>
internal sealed record BuildArtifactRefWriteResult
{
    private BuildArtifactRefWriteResult (
        BuildArtifactRef? artifact,
        ExecutionError? error)
    {
        Artifact = artifact;
        Error = error;
    }

    /// <summary> Gets the written artifact reference when writing succeeded. </summary>
    public BuildArtifactRef? Artifact { get; }

    /// <summary> Gets the write error when writing failed. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether the artifact was written successfully. </summary>
    public bool IsSuccess => Artifact != null && Error == null;

    /// <summary> Creates a successful write result. </summary>
    public static BuildArtifactRefWriteResult Success (BuildArtifactRef artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new BuildArtifactRefWriteResult(artifact, null);
    }

    /// <summary> Creates a failed write result. </summary>
    public static BuildArtifactRefWriteResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildArtifactRefWriteResult(null, error);
    }
}
