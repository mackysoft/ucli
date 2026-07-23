using MackySoft.FileSystem;

namespace MackySoft.Ucli.Tests.Helpers.Git;

internal sealed class RecordingGitCommandClient : IGitCommandClient
{
    public GitCommandTextResult CurrentWorktreeRootResult { get; set; } = GitCommandTextResult.Success(null);

    public GitCommandTextResult CurrentProjectRelativePathResult { get; set; } = GitCommandTextResult.Success(null);

    public GitCommandTextResult WorktreeListPorcelainResult { get; set; } = GitCommandTextResult.Success(null);

    public List<AbsolutePath> CurrentWorktreeRootPaths { get; } = [];

    public List<AbsolutePath> CurrentProjectRelativePathPaths { get; } = [];

    public List<AbsolutePath> WorktreeListPorcelainPaths { get; } = [];

    public List<TimeSpan> CurrentWorktreeRootTimeouts { get; } = [];

    public Func<AbsolutePath, TimeSpan, CancellationToken, ValueTask<GitCommandTextResult>>? CurrentWorktreeRootHandler { get; set; }

    public ValueTask<GitCommandTextResult> GetCurrentWorktreeRootAsync (
        AbsolutePath path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        CurrentWorktreeRootPaths.Add(path);
        CurrentWorktreeRootTimeouts.Add(timeout);
        if (CurrentWorktreeRootHandler != null)
        {
            return CurrentWorktreeRootHandler(path, timeout, cancellationToken);
        }

        return ValueTask.FromResult(CurrentWorktreeRootResult);
    }

    public ValueTask<GitCommandTextResult> GetCurrentProjectRelativePathAsync (
        AbsolutePath path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        CurrentProjectRelativePathPaths.Add(path);
        return ValueTask.FromResult(CurrentProjectRelativePathResult);
    }

    public ValueTask<GitCommandTextResult> GetWorktreeListPorcelainAsync (
        AbsolutePath path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        WorktreeListPorcelainPaths.Add(path);
        return ValueTask.FromResult(WorktreeListPorcelainResult);
    }
}
