namespace MackySoft.Ucli.Tests.Helpers.Git;

internal sealed class RecordingGitCommandClient : IGitCommandClient
{
    public GitCommandTextResult CurrentWorktreeRootResult { get; set; } = GitCommandTextResult.Success(null);

    public GitCommandTextResult CurrentProjectRelativePathResult { get; set; } = GitCommandTextResult.Success(null);

    public GitCommandTextResult WorktreeListPorcelainResult { get; set; } = GitCommandTextResult.Success(null);

    public List<string> CurrentWorktreeRootPaths { get; } = [];

    public List<string> CurrentProjectRelativePathPaths { get; } = [];

    public List<string> WorktreeListPorcelainPaths { get; } = [];

    public List<TimeSpan> CurrentWorktreeRootTimeouts { get; } = [];

    public Func<string, TimeSpan, CancellationToken, ValueTask<GitCommandTextResult>>? CurrentWorktreeRootHandler { get; set; }

    public ValueTask<GitCommandTextResult> GetCurrentWorktreeRootAsync (
        string path,
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
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        CurrentProjectRelativePathPaths.Add(path);
        return ValueTask.FromResult(CurrentProjectRelativePathResult);
    }

    public ValueTask<GitCommandTextResult> GetWorktreeListPorcelainAsync (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        WorktreeListPorcelainPaths.Add(path);
        return ValueTask.FromResult(WorktreeListPorcelainResult);
    }
}
