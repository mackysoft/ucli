namespace MackySoft.Ucli.Application.Shared.Git;

/// <summary> Represents one Git worktree entry parsed from <c>git worktree list --porcelain</c>. </summary>
/// <param name="WorktreePath"> The Git worktree root path. </param>
/// <param name="Head"> The HEAD commit hash. </param>
/// <param name="BranchRef"> The attached branch ref when available; otherwise <see langword="null" />. </param>
internal sealed record GitWorktreeInfo (
    string WorktreePath,
    string Head,
    string? BranchRef);
