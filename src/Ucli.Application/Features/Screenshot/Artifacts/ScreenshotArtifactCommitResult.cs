using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Represents the result of committing one screenshot artifact. </summary>
internal sealed record ScreenshotArtifactCommitResult
{
    private ScreenshotArtifactCommitResult (
        ScreenshotArtifact? artifact,
        ExecutionError? error)
    {
        Artifact = artifact;
        Error = error;
    }

    /// <summary> Gets the committed artifact on success. </summary>
    public ScreenshotArtifact? Artifact { get; }

    /// <summary> Gets the structured commit error on failure. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether commit succeeded. </summary>
    public bool IsSuccess => Artifact != null;

    /// <summary> Creates a successful commit result. </summary>
    public static ScreenshotArtifactCommitResult Success (ScreenshotArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new ScreenshotArtifactCommitResult(artifact, null);
    }

    /// <summary> Creates a failed commit result. </summary>
    public static ScreenshotArtifactCommitResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ScreenshotArtifactCommitResult(null, error);
    }
}
