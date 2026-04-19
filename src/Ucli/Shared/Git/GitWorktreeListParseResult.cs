using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Git;

/// <summary> Represents the result of parsing <c>git worktree list --porcelain</c> output. </summary>
/// <param name="Worktrees"> The parsed Git worktree entries on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record GitWorktreeListParseResult (
    IReadOnlyList<GitWorktreeInfo>? Worktrees,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether parsing succeeded. </summary>
    public bool IsSuccess => Worktrees is not null && Error is null;

    /// <summary> Creates a successful parse result. </summary>
    /// <param name="worktrees"> The parsed Git worktree entries. </param>
    /// <returns> The successful result. </returns>
    public static GitWorktreeListParseResult Success (IReadOnlyList<GitWorktreeInfo> worktrees)
    {
        ArgumentNullException.ThrowIfNull(worktrees);
        return new GitWorktreeListParseResult(worktrees, null);
    }

    /// <summary> Creates a failed parse result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    public static GitWorktreeListParseResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GitWorktreeListParseResult(null, error);
    }
}