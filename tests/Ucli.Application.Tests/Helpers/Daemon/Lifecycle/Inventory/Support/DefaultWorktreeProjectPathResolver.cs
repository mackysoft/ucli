using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class DefaultWorktreeProjectPathResolver : IWorktreeProjectPathResolver
{
    public AbsolutePath ResolveCandidateProjectPath (
        AbsolutePath worktreePath,
        RootRelativePath projectRelativePath)
    {
        return ContainedPath.Create(worktreePath, projectRelativePath).Target;
    }
}
