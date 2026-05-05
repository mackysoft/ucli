using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Inventory;

/// <summary> Resolves Unity project paths from Git worktree paths using host filesystem path rules. </summary>
internal sealed class WorktreeProjectPathResolver : IWorktreeProjectPathResolver
{
    /// <inheritdoc />
    public string ResolveCandidateProjectPath (
        string worktreePath,
        string projectRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativePath);

        return string.Equals(projectRelativePath, ".", StringComparison.Ordinal)
            ? worktreePath
            : Path.Combine(worktreePath, projectRelativePath);
    }
}
