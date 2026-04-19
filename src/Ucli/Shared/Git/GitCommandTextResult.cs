using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Git;

/// <summary> Represents one raw text result returned from a Git command. </summary>
/// <param name="Text"> The command text output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record GitCommandTextResult (
    string? Text,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the Git command succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful Git command text result. </summary>
    /// <param name="text"> The command text output. </param>
    /// <returns> The successful result. </returns>
    public static GitCommandTextResult Success (string? text)
    {
        return new GitCommandTextResult(text, null);
    }

    /// <summary> Creates a failed Git command text result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    public static GitCommandTextResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GitCommandTextResult(null, error);
    }
}