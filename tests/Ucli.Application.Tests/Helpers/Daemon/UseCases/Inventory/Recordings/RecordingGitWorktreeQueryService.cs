using MackySoft.Ucli.Application.Shared.Git;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingGitWorktreeQueryService : IGitWorktreeQueryService
{
    private readonly GitWorktreeQueryResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingGitWorktreeQueryService (GitWorktreeQueryResult result)
    {
        this.result = result;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public IReadOnlyList<string> QueryPaths => invocations
        .Select(static invocation => invocation.Path)
        .ToArray();

    public ValueTask<GitWorktreeQueryResult> GetWorktreeInfoAsync (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(path, timeout, cancellationToken));
        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        string Path,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
