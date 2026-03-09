using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Git;

namespace MackySoft.Ucli.Tests.Git;

public sealed class GitWorktreeQueryServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetWorktreeInfo_WhenSuccessful_ReturnsSnapshot ()
    {
        var commandClient = new StubGitCommandClient
        {
            CurrentWorktreeRootResult = GitCommandTextResult.Success("/repo/wt-current" + Environment.NewLine),
            CurrentProjectRelativePathResult = GitCommandTextResult.Success("UnityProject/" + Environment.NewLine),
            WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
        };
        var parser = new StubGitWorktreeListPorcelainParser
        {
            Result = GitWorktreeListParseResult.Success(
            [
                new GitWorktreeInfo("/repo/wt-b", "bbbbbbbb", "refs/heads/feature/worktree-b"),
                new GitWorktreeInfo("/repo/wt-a", "aaaaaaaa", null),
            ]),
        };
        var service = new GitWorktreeQueryService(commandClient, parser);

        var result = await service.GetWorktreeInfo(
            "/repo/wt-current/UnityProject",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<GitWorktreeQueryOutput>(result.Output);
        Assert.Equal("/repo/wt-current", output.CurrentWorktreeRoot);
        Assert.Equal("UnityProject", output.ProjectRelativePath);
        Assert.Equal(2, output.Worktrees.Count);
        Assert.Equal(["/repo/wt-current/UnityProject"], commandClient.CurrentWorktreeRootPaths);
        Assert.Equal(["/repo/wt-current/UnityProject"], commandClient.CurrentProjectRelativePathPaths);
        Assert.Equal(["/repo/wt-current/UnityProject"], commandClient.WorktreeListPorcelainPaths);
        Assert.Equal(["porcelain-output"], parser.Inputs);
        Assert.Equal(TimeSpan.FromSeconds(10), commandClient.CurrentWorktreeRootTimeouts.Single());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetWorktreeInfo_WhenShowPrefixReturnsEmpty_ReturnsDotRelativePath ()
    {
        var service = new GitWorktreeQueryService(
            new StubGitCommandClient
            {
                CurrentWorktreeRootResult = GitCommandTextResult.Success("/repo/wt-current" + Environment.NewLine),
                CurrentProjectRelativePathResult = GitCommandTextResult.Success(null),
                WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
            },
            new StubGitWorktreeListPorcelainParser
            {
                Result = GitWorktreeListParseResult.Success(
                [
                    new GitWorktreeInfo("/repo/wt-current", "cccccccc", "refs/heads/main"),
                ]),
            });

        var result = await service.GetWorktreeInfo(
            "/repo/wt-current",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(".", result.Output!.ProjectRelativePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetWorktreeInfo_WhenParserFails_ReturnsFailure ()
    {
        var service = new GitWorktreeQueryService(
            new StubGitCommandClient
            {
                CurrentWorktreeRootResult = GitCommandTextResult.Success("/repo/wt-current" + Environment.NewLine),
                CurrentProjectRelativePathResult = GitCommandTextResult.Success("UnityProject/" + Environment.NewLine),
                WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
            },
            new StubGitWorktreeListPorcelainParser
            {
                Result = GitWorktreeListParseResult.Failure(ExecutionError.InternalError("missing HEAD")),
            });

        var result = await service.GetWorktreeInfo(
            "/repo/wt-current/UnityProject",
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Equal("missing HEAD", result.Error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetWorktreeInfo_WhenFirstGitCallConsumesBudget_ReturnsTimeoutBeforeSecondCall ()
    {
        var commandClient = new StubGitCommandClient
        {
            CurrentWorktreeRootHandler = async (_, _, cancellationToken) =>
            {
                await Task.Delay(30, cancellationToken);
                return GitCommandTextResult.Success("/repo/wt-current" + Environment.NewLine);
            },
            CurrentProjectRelativePathResult = GitCommandTextResult.Success("UnityProject/" + Environment.NewLine),
            WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
        };
        var service = new GitWorktreeQueryService(
            commandClient,
            new StubGitWorktreeListPorcelainParser());

        var result = await service.GetWorktreeInfo(
            "/repo/wt-current/UnityProject",
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Equal("Timed out before git rev-parse --show-prefix could begin.", result.Error.Message);
        Assert.Single(commandClient.CurrentWorktreeRootPaths);
        Assert.Empty(commandClient.CurrentProjectRelativePathPaths);
        Assert.Empty(commandClient.WorktreeListPorcelainPaths);
    }

    private sealed class StubGitCommandClient : IGitCommandClient
    {
        public GitCommandTextResult CurrentWorktreeRootResult { get; set; } = GitCommandTextResult.Success(null);

        public GitCommandTextResult CurrentProjectRelativePathResult { get; set; } = GitCommandTextResult.Success(null);

        public GitCommandTextResult WorktreeListPorcelainResult { get; set; } = GitCommandTextResult.Success(null);

        public List<string> CurrentWorktreeRootPaths { get; } = new();

        public List<string> CurrentProjectRelativePathPaths { get; } = new();

        public List<string> WorktreeListPorcelainPaths { get; } = new();

        public List<TimeSpan> CurrentWorktreeRootTimeouts { get; } = new();

        public Func<string, TimeSpan, CancellationToken, ValueTask<GitCommandTextResult>>? CurrentWorktreeRootHandler { get; set; }

        public ValueTask<GitCommandTextResult> GetCurrentWorktreeRoot (
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

        public ValueTask<GitCommandTextResult> GetCurrentProjectRelativePath (
            string path,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CurrentProjectRelativePathPaths.Add(path);
            return ValueTask.FromResult(CurrentProjectRelativePathResult);
        }

        public ValueTask<GitCommandTextResult> GetWorktreeListPorcelain (
            string path,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            WorktreeListPorcelainPaths.Add(path);
            return ValueTask.FromResult(WorktreeListPorcelainResult);
        }
    }

    private sealed class StubGitWorktreeListPorcelainParser : IGitWorktreeListPorcelainParser
    {
        public GitWorktreeListParseResult Result { get; set; } = GitWorktreeListParseResult.Success(Array.Empty<GitWorktreeInfo>());

        public List<string?> Inputs { get; } = new();

        public GitWorktreeListParseResult Parse (string? standardOutput)
        {
            Inputs.Add(standardOutput);
            return Result;
        }
    }
}