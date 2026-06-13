using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the result of preparing a build-run artifact layout. </summary>
internal sealed record BuildRunArtifactPreparationResult
{
    private BuildRunArtifactPreparationResult (
        BuildRunArtifactPaths? paths,
        ExecutionError? error)
    {
        Paths = paths;
        Error = error;
    }

    /// <summary> Gets the prepared artifact paths when successful. </summary>
    public BuildRunArtifactPaths? Paths { get; }

    /// <summary> Gets the preparation error when the layout could not be prepared. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether the artifact layout was prepared. </summary>
    public bool IsSuccess => Paths != null && Error == null;

    /// <summary> Creates a successful preparation result. </summary>
    public static BuildRunArtifactPreparationResult Success (BuildRunArtifactPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return new BuildRunArtifactPreparationResult(paths, null);
    }

    /// <summary> Creates a failed preparation result. </summary>
    public static BuildRunArtifactPreparationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildRunArtifactPreparationResult(null, error);
    }
}
