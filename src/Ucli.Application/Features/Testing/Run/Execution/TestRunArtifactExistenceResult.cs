namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Represents required test-run artifact existence validation result. </summary>
internal sealed record TestRunArtifactExistenceResult (string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether all required artifacts exist. </summary>
    public bool IsSuccess => ErrorMessage is null;

    /// <summary> Creates a successful validation result. </summary>
    /// <returns> The successful validation result. </returns>
    public static TestRunArtifactExistenceResult Success ()
    {
        return new TestRunArtifactExistenceResult(ErrorMessage: null);
    }

    /// <summary> Creates a failed validation result. </summary>
    /// <param name="errorMessage"> The validation failure message. </param>
    /// <returns> The failed validation result. </returns>
    public static TestRunArtifactExistenceResult Failure (string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new TestRunArtifactExistenceResult(errorMessage);
    }
}
