using MackySoft.Ucli.Application.Shared.Git;

namespace MackySoft.Ucli.Tests.Helpers.Git;

internal sealed class RecordingGitWorktreeListPorcelainParser : IGitWorktreeListPorcelainParser
{
    public GitWorktreeListParseResult Result { get; set; } = GitWorktreeListParseResult.Success(Array.Empty<GitWorktreeInfo>());

    public List<string?> Inputs { get; } = [];

    public GitWorktreeListParseResult Parse (string? standardOutput)
    {
        Inputs.Add(standardOutput);
        return Result;
    }
}
