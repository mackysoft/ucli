using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Testing.Run.Artifacts;

/// <summary> Represents the result of preparing test-run artifact directories and initial metadata. </summary>
/// <param name="Session"> The prepared artifacts session on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record ArtifactsPreparationResult (
    ArtifactsSession? Session,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether artifact preparation succeeded. </summary>
    public bool IsSuccess => Session is not null && Error is null;

    /// <summary> Creates a successful preparation result. </summary>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <returns> The successful preparation result. </returns>
    public static ArtifactsPreparationResult Success (ArtifactsSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new ArtifactsPreparationResult(session, null);
    }

    /// <summary> Creates a failed preparation result. </summary>
    /// <param name="error"> The structured failure error. </param>
    /// <returns> The failed preparation result. </returns>
    public static ArtifactsPreparationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ArtifactsPreparationResult(null, error);
    }
}