using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Git;

/// <summary> Implements Git command execution and process-result classification. </summary>
internal sealed class GitCommandClient : IGitCommandClient
{
    private const string GitExecutableName = "git";

    private readonly IProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="GitCommandClient" /> class. </summary>
    /// <param name="processRunner"> The process-runner dependency. </param>
    public GitCommandClient (IProcessRunner processRunner)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <summary> Gets the current Git worktree root text from <c>git rev-parse --show-toplevel</c>. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git command text result. </returns>
    public async ValueTask<GitCommandTextResult> GetCurrentWorktreeRoot (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await RunRevParse(
                path,
                "--show-toplevel",
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Gets the current project-relative path text from <c>git rev-parse --show-prefix</c>. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git command text result. </returns>
    public async ValueTask<GitCommandTextResult> GetCurrentProjectRelativePath (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await RunRevParse(
                path,
                "--show-prefix",
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Gets the porcelain worktree list text from <c>git worktree list --porcelain</c>. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git command text result. </returns>
    public async ValueTask<GitCommandTextResult> GetWorktreeListPorcelain (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var processResult = await RunGitCommand(
                path,
                ["worktree", "list", "--porcelain"],
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        return CreateWorktreeListResult(processResult);
    }

    /// <summary> Executes one <c>git rev-parse</c> command. </summary>
    /// <param name="path"> The path anchored inside the current Git worktree. </param>
    /// <param name="option"> The rev-parse option. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Git command text result. </returns>
    private async ValueTask<GitCommandTextResult> RunRevParse (
        string path,
        string option,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var processResult = await RunGitCommand(
                path,
                ["rev-parse", option],
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        return CreateRevParseResult(processResult);
    }

    /// <summary> Executes one Git command using the specified path as anchor. </summary>
    /// <param name="path"> The path passed to Git <c>-C</c>. </param>
    /// <param name="arguments"> The Git arguments after <c>-C &lt;path&gt;</c>. </param>
    /// <param name="timeout"> The timeout budget for this Git command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The raw process execution result. </returns>
    private async ValueTask<ProcessRunResult> RunGitCommand (
        string path,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var gitArguments = new List<string>(arguments.Count + 2)
        {
            "-C",
            path,
        };
        gitArguments.AddRange(arguments);

        var processResult = await processRunner.RunAsync(
                new ProcessRunRequest(
                    FileName: GitExecutableName,
                    Arguments: gitArguments,
                    Timeout: timeout,
                    CaptureStandardOutput: true),
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return processResult;
    }

    /// <summary> Maps one <c>rev-parse</c> process result to a Git command text result. </summary>
    /// <param name="processResult"> The raw process result. </param>
    /// <returns> The normalized Git command text result. </returns>
    private static GitCommandTextResult CreateRevParseResult (ProcessRunResult processResult)
    {
        switch (processResult.Status)
        {
            case ProcessRunStatus.StartFailed:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    processResult.ErrorMessage ?? "Failed to start git rev-parse."));

            case ProcessRunStatus.TimedOut:
                return GitCommandTextResult.Failure(ExecutionError.Timeout(
                    processResult.ErrorMessage ?? "Git rev-parse timed out."));

            case ProcessRunStatus.Canceled:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    processResult.ErrorMessage ?? "Git rev-parse was canceled."));

            case ProcessRunStatus.Exited when processResult.ExitCode == 0:
                return GitCommandTextResult.Success(processResult.StandardOutput);

            case ProcessRunStatus.Exited:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    processResult.ErrorMessage ?? "Git rev-parse failed."));

            default:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    "Git rev-parse returned an unknown process status."));
        }
    }

    /// <summary> Maps one <c>git worktree list</c> process result to a Git command text result. </summary>
    /// <param name="processResult"> The raw process result. </param>
    /// <returns> The normalized Git command text result. </returns>
    private static GitCommandTextResult CreateWorktreeListResult (ProcessRunResult processResult)
    {
        switch (processResult.Status)
        {
            case ProcessRunStatus.StartFailed:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    processResult.ErrorMessage ?? "Failed to start git worktree list."));

            case ProcessRunStatus.TimedOut:
                return GitCommandTextResult.Failure(ExecutionError.Timeout(
                    processResult.ErrorMessage ?? "Git worktree list timed out."));

            case ProcessRunStatus.Canceled:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    processResult.ErrorMessage ?? "Git worktree list was canceled."));

            case ProcessRunStatus.Exited when processResult.ExitCode == 0:
                return GitCommandTextResult.Success(processResult.StandardOutput);

            case ProcessRunStatus.Exited:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    processResult.ErrorMessage ?? "Git worktree list failed."));

            default:
                return GitCommandTextResult.Failure(ExecutionError.InternalError(
                    "Git worktree list returned an unknown process status."));
        }
    }
}
