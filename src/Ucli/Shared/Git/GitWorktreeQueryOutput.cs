namespace MackySoft.Ucli.Shared.Git;

/// <summary> Represents normalized Git worktree metadata queried for one current path. </summary>
/// <param name="CurrentWorktreeRoot"> The current Git worktree root path. </param>
/// <param name="ProjectRelativePath"> The current path relative to the current Git worktree root. </param>
/// <param name="Worktrees"> The parsed Git worktree entries. </param>
internal sealed record GitWorktreeQueryOutput (
    string CurrentWorktreeRoot,
    string ProjectRelativePath,
    IReadOnlyList<GitWorktreeInfo> Worktrees);