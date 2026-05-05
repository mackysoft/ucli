using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Git;

/// <summary> Implements parsing for <c>git worktree list --porcelain</c> output. </summary>
internal sealed class GitWorktreeListPorcelainParser : IGitWorktreeListPorcelainParser
{
    /// <summary> Parses porcelain text into Git worktree metadata. </summary>
    /// <param name="standardOutput"> The porcelain text returned from Git. </param>
    /// <returns> The parse result. </returns>
    public GitWorktreeListParseResult Parse (string? standardOutput)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(standardOutput, out var normalizedOutput))
        {
            return GitWorktreeListParseResult.Failure(ExecutionError.InternalError(
                "Git worktree list returned empty output."));
        }

        var entries = new List<GitWorktreeInfo>();
        string? currentWorktreePath = null;
        string? currentHead = null;
        string? currentBranchRef = null;
        var hasDetachedMarker = false;
        var lines = normalizedOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0)
            {
                if (!TryFinalizeWorktreeEntry(
                        ref currentWorktreePath,
                        ref currentHead,
                        ref currentBranchRef,
                        ref hasDetachedMarker,
                        entries,
                        out var error))
                {
                    return GitWorktreeListParseResult.Failure(error!);
                }

                continue;
            }

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                if (currentWorktreePath != null
                    && !TryFinalizeWorktreeEntry(
                        ref currentWorktreePath,
                        ref currentHead,
                        ref currentBranchRef,
                        ref hasDetachedMarker,
                        entries,
                        out var error))
                {
                    return GitWorktreeListParseResult.Failure(error!);
                }

                currentWorktreePath = line.Substring("worktree ".Length);
                continue;
            }

            if (line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                currentHead = line.Substring("HEAD ".Length);
                continue;
            }

            if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                currentBranchRef = line.Substring("branch ".Length);
                continue;
            }

            if (string.Equals(line, "detached", StringComparison.Ordinal))
            {
                hasDetachedMarker = true;
            }
        }

        if (!TryFinalizeWorktreeEntry(
                ref currentWorktreePath,
                ref currentHead,
                ref currentBranchRef,
                ref hasDetachedMarker,
                entries,
                out var finalError))
        {
            return GitWorktreeListParseResult.Failure(finalError!);
        }

        return GitWorktreeListParseResult.Success(entries);
    }

    /// <summary> Finalizes one in-progress Git worktree entry. </summary>
    /// <param name="worktreePath"> The mutable worktree-path accumulator. </param>
    /// <param name="head"> The mutable HEAD accumulator. </param>
    /// <param name="branchRef"> The mutable branch-ref accumulator. </param>
    /// <param name="hasDetachedMarker"> The mutable detached-marker accumulator. </param>
    /// <param name="entries"> The parsed entry list. </param>
    /// <param name="error"> The structured parse error when finalization fails. </param>
    /// <returns> <see langword="true" /> when finalization succeeds or no entry is pending; otherwise <see langword="false" />. </returns>
    private static bool TryFinalizeWorktreeEntry (
        ref string? worktreePath,
        ref string? head,
        ref string? branchRef,
        ref bool hasDetachedMarker,
        ICollection<GitWorktreeInfo> entries,
        out ExecutionError? error)
    {
        if (worktreePath == null && head == null && branchRef == null && !hasDetachedMarker)
        {
            error = null;
            return true;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(worktreePath, out var normalizedWorktreePath))
        {
            error = ExecutionError.InternalError("Git worktree list entry is missing a worktree path.");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(head, out var normalizedHead))
        {
            error = ExecutionError.InternalError($"Git worktree list entry is missing HEAD for '{normalizedWorktreePath}'.");
            return false;
        }

        if (branchRef != null && !StringValueNormalizer.TryTrimToNonEmpty(branchRef, out branchRef))
        {
            error = ExecutionError.InternalError($"Git worktree list entry has an invalid branch ref for '{normalizedWorktreePath}'.");
            return false;
        }

        try
        {
            entries.Add(new GitWorktreeInfo(
                WorktreePath: Path.GetFullPath(normalizedWorktreePath),
                Head: normalizedHead,
                BranchRef: hasDetachedMarker ? null : branchRef));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            error = ExecutionError.InternalError(
                $"Git worktree list returned an invalid worktree path '{normalizedWorktreePath}'. {exception.Message}");
            return false;
        }

        worktreePath = null;
        head = null;
        branchRef = null;
        hasDetachedMarker = false;
        error = null;
        return true;
    }
}
