using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class DefaultWorktreeProjectPathResolver : IWorktreeProjectPathResolver
{
    public string ResolveCandidateProjectPath (
        string worktreePath,
        string projectRelativePath)
    {
        return string.Equals(projectRelativePath, ".", StringComparison.Ordinal)
            ? worktreePath
            : Path.Combine(worktreePath, projectRelativePath);
    }
}
