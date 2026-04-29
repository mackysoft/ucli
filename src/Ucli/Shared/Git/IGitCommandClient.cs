namespace MackySoft.Ucli.Shared.Git;

/// <summary> Gets raw text results from Git commands anchored at one path. </summary>
internal interface IGitCommandClient
{
    /// <summary> Gets the current Git worktree root text from <c>git rev-parse --show-toplevel</c>. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git command text result. </returns>
    ValueTask<GitCommandTextResult> GetCurrentWorktreeRoot (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary> Gets the current project-relative path text from <c>git rev-parse --show-prefix</c>. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git command text result. </returns>
    ValueTask<GitCommandTextResult> GetCurrentProjectRelativePath (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary> Gets the porcelain worktree list text from <c>git worktree list --porcelain</c>. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git command text result. </returns>
    ValueTask<GitCommandTextResult> GetWorktreeListPorcelain (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
