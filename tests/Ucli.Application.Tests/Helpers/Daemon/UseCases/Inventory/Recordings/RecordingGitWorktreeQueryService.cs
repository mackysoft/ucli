using MackySoft.FileSystem;
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

    public IReadOnlyList<AbsolutePath> QueryPaths => invocations
        .Select(static invocation => invocation.Path)
        .ToArray();

    public ValueTask<GitWorktreeQueryResult> GetWorktreeInfoAsync (
        AbsolutePath path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(path, timeout, cancellationToken));
        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        AbsolutePath Path,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
