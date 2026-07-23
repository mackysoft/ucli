using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;

/// <summary> Resolves candidate Unity project paths from Git worktree information. </summary>
internal interface IWorktreeProjectPathResolver
{
    /// <summary> Resolves one candidate Unity project path. </summary>
    /// <param name="worktreePath"> The Git worktree root path. </param>
    /// <param name="projectRelativePath"> The project path relative to the Git worktree root. </param>
    /// <returns> The candidate Unity project path. </returns>
    AbsolutePath ResolveCandidateProjectPath (
        AbsolutePath worktreePath,
        RootRelativePath projectRelativePath);
}
