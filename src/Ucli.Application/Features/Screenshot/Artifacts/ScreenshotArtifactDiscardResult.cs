using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Represents the result of discarding one screenshot staging layout. </summary>
internal sealed record ScreenshotArtifactDiscardResult (ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether discard succeeded. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful discard result. </summary>
    public static ScreenshotArtifactDiscardResult Success ()
    {
        return new ScreenshotArtifactDiscardResult(Error: null);
    }

    /// <summary> Creates a failed discard result. </summary>
    public static ScreenshotArtifactDiscardResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ScreenshotArtifactDiscardResult(error);
    }
}
