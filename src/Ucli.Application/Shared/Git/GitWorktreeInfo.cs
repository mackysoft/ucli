namespace MackySoft.Ucli.Application.Shared.Git;

/// <summary> Represents one normalized worktree entry discovered by a host adapter. </summary>
/// <param name="WorktreePath"> The Git worktree root path. </param>
/// <param name="Head"> The HEAD commit hash. </param>
/// <param name="BranchRef"> The attached branch ref when available; otherwise <see langword="null" />. </param>
internal sealed record GitWorktreeInfo (
    string WorktreePath,
    string Head,
    string? BranchRef);
