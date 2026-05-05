using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Git;

public sealed class GitWorktreeListPorcelainParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithAttachedAndDetachedWorktrees_ReturnsParsedEntries ()
    {
        var parser = new GitWorktreeListPorcelainParser();
        var expectedFirstWorktreePath = Path.GetFullPath("/repo/wt-b");
        var expectedSecondWorktreePath = Path.GetFullPath("/repo/wt-a");

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
        Assert.Collection(
            result.Worktrees!,
            item =>
            {
                Assert.Equal(expectedFirstWorktreePath, item.WorktreePath);
                Assert.Equal("bbbbbbbb", item.Head);
                Assert.Equal("refs/heads/feature/worktree-b", item.BranchRef);
            },
            item =>
            {
                Assert.Equal(expectedSecondWorktreePath, item.WorktreePath);
                Assert.Equal("aaaaaaaa", item.Head);
                Assert.Null(item.BranchRef);
            });
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
