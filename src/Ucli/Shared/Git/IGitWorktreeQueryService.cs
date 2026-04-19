namespace MackySoft.Ucli.Shared.Git;

/// <summary> Gets Git worktree metadata for one path anchored inside a Git worktree. </summary>
internal interface IGitWorktreeQueryService
{
    /// <summary> Gets the current worktree root, current project-relative path, and sibling worktrees. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git worktree query. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git worktree query result. </returns>
    ValueTask<GitWorktreeQueryResult> GetWorktreeInfo (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}