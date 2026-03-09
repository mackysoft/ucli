using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Git;

/// <summary> Implements Git worktree metadata retrieval by composing Git commands and porcelain parsing. </summary>
internal sealed class GitWorktreeQueryService : IGitWorktreeQueryService
{
    private readonly IGitCommandClient gitCommandClient;

    private readonly IGitWorktreeListPorcelainParser gitWorktreeListPorcelainParser;

    /// <summary> Initializes a new instance of the <see cref="GitWorktreeQueryService" /> class. </summary>
    /// <param name="gitCommandClient"> The Git command-client dependency. </param>
    /// <param name="gitWorktreeListPorcelainParser"> The porcelain parser dependency. </param>
    public GitWorktreeQueryService (
        IGitCommandClient gitCommandClient,
        IGitWorktreeListPorcelainParser gitWorktreeListPorcelainParser)
    {
        this.gitCommandClient = gitCommandClient ?? throw new ArgumentNullException(nameof(gitCommandClient));
        this.gitWorktreeListPorcelainParser = gitWorktreeListPorcelainParser ?? throw new ArgumentNullException(nameof(gitWorktreeListPorcelainParser));
    }

    /// <summary> Gets the current worktree root, current project-relative path, and sibling worktrees. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git worktree query. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git worktree query result. </returns>
    public async ValueTask<GitWorktreeQueryResult> GetWorktreeInfo (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            if (UcliStoragePathResolver.TryResolveRepositoryRoot(path) == null)
            {
                return GitWorktreeQueryResult.Failure(ExecutionError.InvalidArgument(
                    "daemon list requires the target Unity project to be inside a Git worktree."));
            }
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return GitWorktreeQueryResult.Failure(ExecutionError.InvalidArgument(
                $"Git worktree path is invalid. {exception.Message}"));
        }

        var deadline = ExecutionDeadline.Start(timeout);
        var currentWorktreeRootResult = await gitCommandClient.GetCurrentWorktreeRoot(path, timeout, cancellationToken).ConfigureAwait(false);
        if (!currentWorktreeRootResult.IsSuccess)
        {
            return GitWorktreeQueryResult.Failure(currentWorktreeRootResult.Error!);
        }

        var normalizedWorktreeRootResult = NormalizeCurrentWorktreeRoot(currentWorktreeRootResult.Text);
        if (!normalizedWorktreeRootResult.IsSuccess)
        {
            return GitWorktreeQueryResult.Failure(normalizedWorktreeRootResult.Error!);
        }

        if (!TryGetRemainingTimeout(
                deadline,
                "Timed out before git rev-parse --show-prefix could begin.",
                out var projectRelativePathTimeout,
                out var projectRelativePathTimeoutError))
        {
            return GitWorktreeQueryResult.Failure(projectRelativePathTimeoutError!);
        }

        var projectRelativePathResult = await gitCommandClient.GetCurrentProjectRelativePath(path, projectRelativePathTimeout, cancellationToken).ConfigureAwait(false);
        if (!projectRelativePathResult.IsSuccess)
        {
            return GitWorktreeQueryResult.Failure(projectRelativePathResult.Error!);
        }

        var normalizedProjectRelativePath = NormalizeCurrentProjectRelativePath(projectRelativePathResult.Text);
        if (!TryGetRemainingTimeout(
                deadline,
                "Timed out before git worktree list could begin.",
                out var worktreeListTimeout,
                out var worktreeListTimeoutError))
        {
            return GitWorktreeQueryResult.Failure(worktreeListTimeoutError!);
        }

        var worktreeListTextResult = await gitCommandClient.GetWorktreeListPorcelain(path, worktreeListTimeout, cancellationToken).ConfigureAwait(false);
        if (!worktreeListTextResult.IsSuccess)
        {
            return GitWorktreeQueryResult.Failure(worktreeListTextResult.Error!);
        }

        var worktreeListParseResult = gitWorktreeListPorcelainParser.Parse(worktreeListTextResult.Text);
        if (!worktreeListParseResult.IsSuccess)
        {
            return GitWorktreeQueryResult.Failure(worktreeListParseResult.Error!);
        }

        return GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
            CurrentWorktreeRoot: normalizedWorktreeRootResult.WorktreeRoot!,
            ProjectRelativePath: normalizedProjectRelativePath,
            Worktrees: worktreeListParseResult.Worktrees!));
    }

    /// <summary> Gets remaining timeout from the shared execution deadline. </summary>
    /// <param name="deadline"> The shared execution deadline. </param>
    /// <param name="timeoutMessage"> The timeout message to emit when budget is exhausted. </param>
    /// <param name="remainingTimeout"> The remaining timeout budget. </param>
    /// <param name="error"> The timeout error when budget is exhausted; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when remaining timeout is available; otherwise <see langword="false" />. </returns>
    private static bool TryGetRemainingTimeout (
        ExecutionDeadline deadline,
        string timeoutMessage,
        out TimeSpan remainingTimeout,
        out ExecutionError? error)
    {
        if (deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            error = null;
            return true;
        }

        error = ExecutionError.Timeout(timeoutMessage);
        return false;
    }

    /// <summary> Normalizes current-worktree-root text to an absolute path. </summary>
    /// <param name="text"> The current-worktree-root text returned from Git. </param>
    /// <returns> The normalization result. </returns>
    private static GitWorktreeRootNormalizationResult NormalizeCurrentWorktreeRoot (string? text)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(text, out var worktreeRoot))
        {
            return GitWorktreeRootNormalizationResult.Failure(ExecutionError.InternalError(
                "Git rev-parse returned an empty worktree root."));
        }

        try
        {
            return GitWorktreeRootNormalizationResult.Success(Path.GetFullPath(worktreeRoot));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return GitWorktreeRootNormalizationResult.Failure(ExecutionError.InternalError(
                $"Git rev-parse returned an invalid worktree root path. {exception.Message}"));
        }
    }

    /// <summary> Normalizes current-project-relative-path text. </summary>
    /// <param name="text"> The current-project-relative-path text returned from Git. </param>
    /// <returns> The normalized project-relative path. </returns>
    private static string NormalizeCurrentProjectRelativePath (string? text)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(text, out var projectRelativePath))
        {
            return ".";
        }

        projectRelativePath = PathStringNormalizer.TrimTrailingDirectorySeparators(
            PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(projectRelativePath));
        return projectRelativePath.Length == 0 ? "." : projectRelativePath;
    }

    /// <summary> Represents the result of normalizing current-worktree-root text. </summary>
    /// <param name="WorktreeRoot"> The normalized current worktree root on success; otherwise <see langword="null" />. </param>
    /// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
    private readonly record struct GitWorktreeRootNormalizationResult (
        string? WorktreeRoot,
        ExecutionError? Error)
    {
        /// <summary> Gets a value indicating whether normalization succeeded. </summary>
        public bool IsSuccess => WorktreeRoot is not null && Error is null;

        /// <summary> Creates a successful current-worktree-root normalization result. </summary>
        /// <param name="worktreeRoot"> The normalized current worktree root. </param>
        /// <returns> The successful result. </returns>
        public static GitWorktreeRootNormalizationResult Success (string worktreeRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(worktreeRoot);
            return new GitWorktreeRootNormalizationResult(worktreeRoot, null);
        }

        /// <summary> Creates a failed current-worktree-root normalization result. </summary>
        /// <param name="error"> The structured error. </param>
        /// <returns> The failed result. </returns>
        public static GitWorktreeRootNormalizationResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new GitWorktreeRootNormalizationResult(null, error);
        }
    }
}