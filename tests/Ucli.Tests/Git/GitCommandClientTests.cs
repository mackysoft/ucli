using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Git;

namespace MackySoft.Ucli.Tests.Git;

public sealed class GitCommandClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetCurrentWorktreeRoot_WhenSuccessful_UsesGitCommandCap ()
    {
        var processRunner = new StubProcessRunner(
        [
            ProcessRunResult.Exited(0, standardOutput: "/repo/wt-current" + Environment.NewLine),
        ]);
        var client = new GitCommandClient(processRunner);

        var result = await client.GetCurrentWorktreeRoot(
            "/repo/wt-current/UnityProject",
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/repo/wt-current" + Environment.NewLine, result.Text);
        Assert.Equal(["-C", "/repo/wt-current/UnityProject", "rev-parse", "--show-toplevel"], processRunner.Requests.Single().Arguments);
        Assert.Equal(TimeSpan.FromSeconds(5), processRunner.Requests.Single().Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetCurrentWorktreeRoot_WhenTimeoutIsShorterThanGitCommandCap_UsesProvidedTimeout ()
    {
        var processRunner = new StubProcessRunner(
        [
            ProcessRunResult.Exited(0, standardOutput: "/repo/wt-current" + Environment.NewLine),
        ]);
        var client = new GitCommandClient(processRunner);

        var result = await client.GetCurrentWorktreeRoot(
            "/repo/wt-current/UnityProject",
            TimeSpan.FromMilliseconds(200),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(200), processRunner.Requests.Single().Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetCurrentProjectRelativePath_WhenRevParseFails_ReturnsInvalidArgument ()
    {
        var client = new GitCommandClient(new StubProcessRunner(
        [
            ProcessRunResult.Exited(128, "fatal: not a git repository"),
        ]));

        var result = await client.GetCurrentProjectRelativePath(
            "/repo/not-git/UnityProject",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("inside a Git worktree", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetCurrentProjectRelativePath_WhenRevParseFailsForOperationalReason_ReturnsInternalError ()
    {
        var client = new GitCommandClient(new StubProcessRunner(
        [
            ProcessRunResult.Exited(128, "fatal: detected dubious ownership in repository"),
        ]));

        var result = await client.GetCurrentProjectRelativePath(
            "/repo/not-git/UnityProject",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("dubious ownership", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetWorktreeListPorcelain_WhenGitFails_ReturnsInternalError ()
    {
        var client = new GitCommandClient(new StubProcessRunner(
        [
            ProcessRunResult.Exited(1, "fatal: worktree list failed"),
        ]));

        var result = await client.GetWorktreeListPorcelain(
            "/repo/wt-current/UnityProject",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("worktree list failed", result.Error.Message, StringComparison.Ordinal);
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        private readonly Queue<ProcessRunResult> results;

        public StubProcessRunner (IEnumerable<ProcessRunResult> results)
        {
            this.results = new Queue<ProcessRunResult>(results);
        }

        public List<ProcessRunRequest> Requests { get; } = new();

        public Task<ProcessRunResult> RunAsync (
            ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(results.Dequeue());
        }
    }
}