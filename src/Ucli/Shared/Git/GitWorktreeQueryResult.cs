using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Git;

/// <summary> Represents the result of querying Git worktree metadata. </summary>
/// <param name="Output"> The Git worktree query output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record GitWorktreeQueryResult (
    GitWorktreeQueryOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the query succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful Git worktree query result. </summary>
    /// <param name="output"> The normalized Git worktree query output. </param>
    /// <returns> The successful result. </returns>
    public static GitWorktreeQueryResult Success (GitWorktreeQueryOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new GitWorktreeQueryResult(output, null);
    }

    /// <summary> Creates a failed Git worktree query result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    public static GitWorktreeQueryResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GitWorktreeQueryResult(null, error);
    }
}
