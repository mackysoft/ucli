using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Represents the result of preparing one capture-scoped screenshot artifact lease. </summary>
internal sealed record ScreenshotArtifactPreparationResult
{
    private ScreenshotArtifactPreparationResult (
        IScreenshotArtifactLease? lease,
        ExecutionError? error)
    {
        Lease = lease;
        Error = error;
    }

    /// <summary> Gets the prepared capture-scoped artifact lease on success. </summary>
    public IScreenshotArtifactLease? Lease { get; }

    /// <summary> Gets the structured preparation error on failure. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether preparation succeeded. </summary>
    public bool IsSuccess => Lease != null;

    /// <summary> Creates a successful preparation result. </summary>
    public static ScreenshotArtifactPreparationResult Success (IScreenshotArtifactLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new ScreenshotArtifactPreparationResult(lease, null);
    }

    /// <summary> Creates a failed preparation result. </summary>
    public static ScreenshotArtifactPreparationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ScreenshotArtifactPreparationResult(null, error);
    }
}
