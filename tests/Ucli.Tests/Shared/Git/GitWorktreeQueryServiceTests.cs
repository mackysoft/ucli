using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Git;
using MackySoft.Ucli.Tests.Helpers.Git;

namespace MackySoft.Ucli.Tests.Git;

public sealed class GitWorktreeQueryServiceTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task GetWorktreeInfo_WhenSuccessful_ReturnsSnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("git-worktree-query", "success");
        var currentProjectPath = CreateGitAnchoredProject(scope, "wt-current", "UnityProject");
        var commandClient = new RecordingGitCommandClient
        {
            CurrentWorktreeRootResult = GitCommandTextResult.Success(Path.GetDirectoryName(currentProjectPath)! + Environment.NewLine),
            CurrentProjectRelativePathResult = GitCommandTextResult.Success("UnityProject/" + Environment.NewLine),
            WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
        };
        var parser = new RecordingGitWorktreeListPorcelainParser
        {
            Result = GitWorktreeListParseResult.Success(
            [
                new GitWorktreeInfo("/repo/wt-b", "bbbbbbbb", "refs/heads/feature/worktree-b"),
                new GitWorktreeInfo("/repo/wt-a", "aaaaaaaa", null),
            ]),
        };
        var service = new GitWorktreeQueryService(commandClient, parser, TimeProvider.System);

        var result = await service.GetWorktreeInfoAsync(
            currentProjectPath,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<GitWorktreeQueryOutput>(result.Output);
        Assert.Equal(Path.GetDirectoryName(currentProjectPath), output.CurrentWorktreeRoot);
        Assert.Equal("UnityProject", output.ProjectRelativePath);
        Assert.Equal(2, output.Worktrees.Count);
        Assert.Equal([currentProjectPath], commandClient.CurrentWorktreeRootPaths);
        Assert.Equal([currentProjectPath], commandClient.CurrentProjectRelativePathPaths);
        Assert.Equal([currentProjectPath], commandClient.WorktreeListPorcelainPaths);
        Assert.Equal(["porcelain-output"], parser.Inputs);
        Assert.Equal(TimeSpan.FromSeconds(10), commandClient.CurrentWorktreeRootTimeouts.Single());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task GetWorktreeInfo_WhenShowPrefixReturnsEmpty_ReturnsDotRelativePath ()
    {
        using var scope = TestDirectories.CreateTempScope("git-worktree-query", "dot-relative-path");
        var currentProjectPath = CreateGitAnchoredProject(scope, "wt-current", projectRelativePath: null);
        var service = new GitWorktreeQueryService(
            new RecordingGitCommandClient
            {
                CurrentWorktreeRootResult = GitCommandTextResult.Success(currentProjectPath + Environment.NewLine),
                CurrentProjectRelativePathResult = GitCommandTextResult.Success(null),
                WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
            },
            new RecordingGitWorktreeListPorcelainParser
            {
                Result = GitWorktreeListParseResult.Success(
                [
                    new GitWorktreeInfo("/repo/wt-current", "cccccccc", "refs/heads/main"),
                ]),
            },
            TimeProvider.System);

        var result = await service.GetWorktreeInfoAsync(
            currentProjectPath,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(".", result.Output!.ProjectRelativePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task GetWorktreeInfo_WhenParserFails_ReturnsFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("git-worktree-query", "parser-failure");
        var currentProjectPath = CreateGitAnchoredProject(scope, "wt-current", "UnityProject");
        var service = new GitWorktreeQueryService(
            new RecordingGitCommandClient
            {
                CurrentWorktreeRootResult = GitCommandTextResult.Success(Path.GetDirectoryName(currentProjectPath)! + Environment.NewLine),
                CurrentProjectRelativePathResult = GitCommandTextResult.Success("UnityProject/" + Environment.NewLine),
                WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
            },
            new RecordingGitWorktreeListPorcelainParser
            {
                Result = GitWorktreeListParseResult.Failure(ExecutionError.InternalError("missing HEAD")),
            },
            TimeProvider.System);

        var result = await service.GetWorktreeInfoAsync(
            currentProjectPath,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Equal("missing HEAD", result.Error.Message);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task GetWorktreeInfo_WhenFirstGitCallConsumesBudget_ReturnsTimeoutBeforeSecondCall ()
    {
        using var scope = TestDirectories.CreateTempScope("git-worktree-query", "first-call-consumes-budget");
        var currentProjectPath = CreateGitAnchoredProject(scope, "wt-current", "UnityProject");
        var timeProvider = new ManualTimeProvider();
        var commandClient = new RecordingGitCommandClient
        {
            CurrentWorktreeRootHandler = (_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(30));
                return ValueTask.FromResult(GitCommandTextResult.Success(Path.GetDirectoryName(currentProjectPath)! + Environment.NewLine));
            },
            CurrentProjectRelativePathResult = GitCommandTextResult.Success("UnityProject/" + Environment.NewLine),
            WorktreeListPorcelainResult = GitCommandTextResult.Success("porcelain-output"),
        };
        var service = new GitWorktreeQueryService(
            commandClient,
            new RecordingGitWorktreeListPorcelainParser(),
            timeProvider);

        var result = await service.GetWorktreeInfoAsync(
            currentProjectPath,
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Equal("Timed out before git rev-parse --show-prefix could begin.", result.Error.Message);
        Assert.Single(commandClient.CurrentWorktreeRootPaths);
        Assert.Empty(commandClient.CurrentProjectRelativePathPaths);
        Assert.Empty(commandClient.WorktreeListPorcelainPaths);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task GetWorktreeInfo_WhenPathIsOutsideGitWorktree_ReturnsInvalidArgumentWithoutGitCommands ()
    {
        using var scope = TestDirectories.CreateTempScope("git-worktree-query", "outside-git-worktree");
        var projectPath = scope.CreateDirectory(Path.Combine("workspace", "UnityProject"));
        var commandClient = new RecordingGitCommandClient();
        var service = new GitWorktreeQueryService(
            commandClient,
            new RecordingGitWorktreeListPorcelainParser(),
            TimeProvider.System);

        var result = await service.GetWorktreeInfoAsync(
            projectPath,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("inside a Git worktree", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(commandClient.CurrentWorktreeRootPaths);
        Assert.Empty(commandClient.CurrentProjectRelativePathPaths);
        Assert.Empty(commandClient.WorktreeListPorcelainPaths);
    }

    private static string CreateGitAnchoredProject (
        TestDirectoryScope scope,
        string worktreeName,
        string? projectRelativePath)
    {
        var worktreeRoot = scope.CreateDirectory(worktreeName);
        scope.CreateDirectory(Path.Combine(worktreeName, ".git"));
        return projectRelativePath == null
            ? worktreeRoot
            : scope.CreateDirectory(Path.Combine(worktreeName, projectRelativePath));
    }

}
