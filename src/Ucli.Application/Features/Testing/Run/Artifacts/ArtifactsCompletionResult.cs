using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Represents the result of completing test-run artifact metadata updates. </summary>
/// <param name="Error"> The structured completion error on failure; otherwise <see langword="null" />. </param>
internal sealed record ArtifactsCompletionResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether artifact completion succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful completion result. </summary>
    /// <returns> The successful completion result. </returns>
    public static ArtifactsCompletionResult Success ()
    {
        return new ArtifactsCompletionResult((ExecutionError?)null);
    }

    /// <summary> Creates a failed completion result. </summary>
    /// <param name="error"> The structured completion error. </param>
    /// <returns> The failed completion result. </returns>
    public static ArtifactsCompletionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ArtifactsCompletionResult(error);
    }
}
