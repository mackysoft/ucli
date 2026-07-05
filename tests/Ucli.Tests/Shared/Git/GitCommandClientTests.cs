using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Git;
using MackySoft.Ucli.Tests.Helpers.Process;

namespace MackySoft.Ucli.Tests.Git;

public sealed class GitCommandClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetCurrentWorktreeRoot_WhenSuccessful_UsesProvidedTimeoutAndRepositoryPathArguments ()
    {
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "/repo/wt-current" + Environment.NewLine),
            ProcessRunResult.Exited(0, standardOutput: "/repo/wt-current" + Environment.NewLine));
        var client = new GitCommandClient(processRunner);

        foreach (var timeout in new[]
        {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(200),
        })
        {
            var result = await client.GetCurrentWorktreeRootAsync(
                "/repo/wt-current/UnityProject",
                timeout,
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("/repo/wt-current" + Environment.NewLine, result.Text);
        }

        GitCommandProcessAssert.WorktreeRootRequestedWithTimeouts(
            processRunner,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetCurrentProjectRelativePath_WhenRevParseFails_ReturnsInternalError ()
    {
        var client = new GitCommandClient(new RecordingProcessRunner(
        [
            ProcessRunResult.Exited(128, "fatal: git リポジトリではありません"),
        ]));

        var result = await client.GetCurrentProjectRelativePathAsync(
            "/repo/not-git/UnityProject",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("リポジトリ", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetCurrentProjectRelativePath_WhenRevParseFailsForOperationalReason_ReturnsInternalError ()
    {
        var client = new GitCommandClient(new RecordingProcessRunner(
        [
            ProcessRunResult.Exited(128, "fatal: detected dubious ownership in repository"),
        ]));

        var result = await client.GetCurrentProjectRelativePathAsync(
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
        var client = new GitCommandClient(new RecordingProcessRunner(
        [
            ProcessRunResult.Exited(1, "fatal: worktree list failed"),
        ]));

        var result = await client.GetWorktreeListPorcelainAsync(
            "/repo/wt-current/UnityProject",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("worktree list failed", result.Error.Message, StringComparison.Ordinal);
    }

}
