using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Represents one verify profile file read result. </summary>
internal sealed record VerifyProfileFileReadResult (
    string? Json,
    string? RepositoryRelativePath,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the file was read successfully. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful read result. </summary>
    public static VerifyProfileFileReadResult Success (
        string json,
        string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);
        return new VerifyProfileFileReadResult(json, repositoryRelativePath, null);
    }

    /// <summary> Creates a failed read result. </summary>
    public static VerifyProfileFileReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new VerifyProfileFileReadResult(null, null, error);
    }
}
