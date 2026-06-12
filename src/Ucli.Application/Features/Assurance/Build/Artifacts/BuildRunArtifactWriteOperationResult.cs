using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the result of writing build-run artifacts. </summary>
internal sealed record BuildRunArtifactWriteOperationResult
{
    private BuildRunArtifactWriteOperationResult (
        BuildRunArtifactWriteResult? result,
        ExecutionError? error)
    {
        Result = result;
        Error = error;
    }

    /// <summary> Gets the artifact accounting result when writing succeeded. </summary>
    public BuildRunArtifactWriteResult? Result { get; }

    /// <summary> Gets the write error when writing failed. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether artifacts were written successfully. </summary>
    public bool IsSuccess => Result != null && Error == null;

    /// <summary> Creates a successful write result. </summary>
    public static BuildRunArtifactWriteOperationResult Success (BuildRunArtifactWriteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new BuildRunArtifactWriteOperationResult(result, null);
    }

    /// <summary> Creates a failed write result. </summary>
    public static BuildRunArtifactWriteOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildRunArtifactWriteOperationResult(null, error);
    }
}
