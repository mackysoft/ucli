using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Git;

namespace MackySoft.Ucli.Tests.Git;

public sealed class GitWorktreeListPorcelainParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithAttachedAndDetachedWorktrees_ReturnsParsedEntries ()
    {
        var parser = new GitWorktreeListPorcelainParser();
        var expectedWorktrees = new[]
        {
            new GitWorktreeInfo(
                Path.GetFullPath("/repo/wt-b"),
                "bbbbbbbb",
                "refs/heads/feature/worktree-b"),
            new GitWorktreeInfo(
                Path.GetFullPath("/repo/wt-a"),
                "aaaaaaaa",
                BranchRef: null),
        };

        var result = parser.Parse(
            """
            worktree /repo/wt-b
            HEAD bbbbbbbb
            branch refs/heads/feature/worktree-b

            worktree /repo/wt-a
            HEAD aaaaaaaa
            detached
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal<GitWorktreeInfo>(expectedWorktrees, result.Worktrees!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenHeadIsMissing_ReturnsInternalError ()
    {
        var parser = new GitWorktreeListPorcelainParser();

        var result = parser.Parse(
            """
            worktree /repo/wt-current
            branch refs/heads/main
            """);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("missing HEAD", result.Error.Message, StringComparison.Ordinal);
    }
}
