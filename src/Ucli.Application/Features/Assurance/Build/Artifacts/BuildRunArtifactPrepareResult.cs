using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents build artifact directory preparation. </summary>
internal sealed record BuildRunArtifactPrepareResult (
    BuildRunArtifactPaths? Paths,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether preparation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful preparation result. </summary>
    public static BuildRunArtifactPrepareResult Success (BuildRunArtifactPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return new BuildRunArtifactPrepareResult(paths, null);
    }

    /// <summary> Creates a failed preparation result. </summary>
    public static BuildRunArtifactPrepareResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildRunArtifactPrepareResult(null, error);
    }
}
