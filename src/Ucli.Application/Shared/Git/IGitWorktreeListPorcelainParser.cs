namespace MackySoft.Ucli.Application.Shared.Git;

/// <summary> Parses <c>git worktree list --porcelain</c> output into normalized worktree metadata. </summary>
internal interface IGitWorktreeListPorcelainParser
{
    /// <summary> Parses porcelain text into Git worktree metadata. </summary>
    /// <param name="standardOutput"> The porcelain text returned from Git. </param>
    /// <returns> The parse result. </returns>
    GitWorktreeListParseResult Parse (string? standardOutput);
}
