using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Represents the result of preparing one screenshot artifact layout. </summary>
internal sealed record ScreenshotArtifactPreparationResult
{
    private ScreenshotArtifactPreparationResult (
        ScreenshotArtifactPaths? paths,
        ExecutionError? error)
    {
        Paths = paths;
        Error = error;
    }

    /// <summary> Gets the prepared paths on success. </summary>
    public ScreenshotArtifactPaths? Paths { get; }

    /// <summary> Gets the structured preparation error on failure. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether preparation succeeded. </summary>
    public bool IsSuccess => Paths != null && Error == null;

    /// <summary> Creates a successful preparation result. </summary>
    public static ScreenshotArtifactPreparationResult Success (ScreenshotArtifactPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return new ScreenshotArtifactPreparationResult(paths, null);
    }

    /// <summary> Creates a failed preparation result. </summary>
    public static ScreenshotArtifactPreparationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ScreenshotArtifactPreparationResult(null, error);
    }
}
