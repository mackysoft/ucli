using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the result of accounting non-metadata build-run artifacts. </summary>
internal sealed record BuildRunArtifactAccountingOperationResult
{
    private BuildRunArtifactAccountingOperationResult (
        BuildRunArtifactAccountingResult? result,
        ExecutionError? error)
    {
        Result = result;
        Error = error;
    }

    /// <summary> Gets the artifact accounting result when accounting succeeded. </summary>
    public BuildRunArtifactAccountingResult? Result { get; }

    /// <summary> Gets the accounting error when accounting failed. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether artifacts were accounted successfully. </summary>
    public bool IsSuccess => Result != null && Error == null;

    /// <summary> Creates a successful accounting result. </summary>
    public static BuildRunArtifactAccountingOperationResult Success (BuildRunArtifactAccountingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new BuildRunArtifactAccountingOperationResult(result, null);
    }

    /// <summary> Creates a failed accounting result. </summary>
    public static BuildRunArtifactAccountingOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildRunArtifactAccountingOperationResult(null, error);
    }
}
