using MackySoft.FileSystem;
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
        var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
        var worktreeB = AbsolutePath.Resolve(currentDirectory, Path.Combine("repo", "wt-b"));
        var worktreeA = AbsolutePath.Resolve(currentDirectory, Path.Combine("repo", "wt-a"));
        var expectedWorktrees = new[]
        {
            new GitWorktreeInfo(
                worktreeB,
                "bbbbbbbb",
                "refs/heads/feature/worktree-b"),
            new GitWorktreeInfo(
                worktreeA,
                "aaaaaaaa",
                BranchRef: null),
        };

        var result = parser.Parse(
            $"""
            worktree {worktreeB.Value}
            HEAD bbbbbbbb
            branch refs/heads/feature/worktree-b

            worktree {worktreeA.Value}
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
        var worktreePath = AbsolutePath.Resolve(
            AbsolutePath.Parse(Environment.CurrentDirectory),
            Path.Combine("repo", "wt-current"));

        var result = parser.Parse(
            $"""
            worktree {worktreePath.Value}
            branch refs/heads/main
            """);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("missing HEAD", result.Error.Message, StringComparison.Ordinal);
    }
}
