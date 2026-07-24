using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Inventory;

/// <summary> Resolves Unity project paths from Git worktree paths using host filesystem path rules. </summary>
internal sealed class WorktreeProjectPathResolver : IWorktreeProjectPathResolver
{
    /// <inheritdoc />
    public AbsolutePath ResolveCandidateProjectPath (
        AbsolutePath worktreePath,
        RootRelativePath projectRelativePath)
    {
        ArgumentNullException.ThrowIfNull(worktreePath);
        ArgumentNullException.ThrowIfNull(projectRelativePath);

        return ContainedPath.Create(worktreePath, projectRelativePath).Target;
    }
}
