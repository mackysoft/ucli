using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Manages the worktree-local supervisor through macOS LaunchAgent ownership. </summary>
internal sealed class LaunchdSupervisorProcessManager
{
    private const string CurrentUserIdExecutablePath = "/usr/bin/id";

    private const string LaunchctlExecutablePath = "/bin/launchctl";

    private readonly IProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="LaunchdSupervisorProcessManager" /> class. </summary>
    /// <param name="processRunner"> The external process-runner dependency. </param>
    public LaunchdSupervisorProcessManager (IProcessRunner processRunner)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <summary> Launches the supervisor for the specified storage root by using <c>launchctl</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="launchCommand"> The resolved relaunch command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch outcome, including any generation lease whose cleanup ownership remains with the caller. </returns>
    public async ValueTask<SupervisorProcessLaunchResult> LaunchAsync (
        string storageRoot,
        SupervisorLaunchCommand launchCommand,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(launchCommand);

        LaunchdSupervisorProcessLaunchLease? launchLease = null;
        try
        {
            var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
            var normalizedStorageRoot = worktreeIdentity.NormalizedStorageRoot;
            var plistPath = UcliStoragePathResolver.ResolveSupervisorLaunchAgentPlistPath(normalizedStorageRoot);
            var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(normalizedStorageRoot);
            var label = BuildLaunchdLabel(worktreeIdentity);
            var userIdResult = await ResolveCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
            if (userIdResult.Error is not null)
            {
                return SupervisorProcessLaunchResult.Failure(userIdResult.Error);
            }

            var userDomain = $"gui/{userIdResult.UserId}";
            var serviceTarget = $"{userDomain}/{label}";
            var bootoutResult = await RunProcessAsync(
                    LaunchctlExecutablePath,
                    ["bootout", "--wait", serviceTarget],
                    captureStandardOutput: false,
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSuccessfulBootout(bootoutResult))
            {
                return SupervisorProcessLaunchResult.Failure(
                    CreateProcessError("remove stale supervisor LaunchAgent", bootoutResult));
            }

            var plistDirectoryPath = Path.GetDirectoryName(plistPath);
            if (!string.IsNullOrWhiteSpace(plistDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(plistDirectoryPath);
            }

            var plistContents = LaunchAgentPlistDocumentFactory.Build(label, launchCommand, normalizedStorageRoot, logPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(plistPath, plistContents + Environment.NewLine, cancellationToken).ConfigureAwait(false);

            launchLease = new LaunchdSupervisorProcessLaunchLease(this, serviceTarget);
            var bootstrapResult = await RunProcessAsync(
                    LaunchctlExecutablePath,
                    ["bootstrap", userDomain, plistPath],
                    captureStandardOutput: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (IsSuccessfulExit(bootstrapResult))
            {
                return SupervisorProcessLaunchResult.Success(launchLease);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await CreateFailureAfterRollbackAsync(
                    CreateProcessError("bootstrap supervisor LaunchAgent", bootstrapResult),
                    launchLease)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (launchLease is null)
            {
                throw;
            }

            var rollbackError = await TryRollbackAsync(launchLease).ConfigureAwait(false);
            if (rollbackError is null)
            {
                throw;
            }

            return SupervisorProcessLaunchResult.FailureWithLease(
                ExecutionError.InternalError(
                    $"Supervisor LaunchAgent bootstrap was canceled. RegistrationRollback={rollbackError.Message}"),
                launchLease);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var launchError = ExecutionError.InternalError($"Failed to launch supervisor with launchctl. {exception.Message}");
            return launchLease is null
                ? SupervisorProcessLaunchResult.Failure(launchError)
                : await CreateFailureAfterRollbackAsync(launchError, launchLease).ConfigureAwait(false);
        }
    }

    /// <summary> Removes the LaunchAgent registration of the currently executing supervisor. </summary>
    /// <param name="storageRoot"> The storage-root path that deterministically identifies the LaunchAgent. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by supervisor retirement. </param>
    /// <returns> One structured error when release fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> ReleaseCurrentProcessRegistrationAsync (
        string storageRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
            var label = BuildLaunchdLabel(worktreeIdentity);
            var userIdResult = await ResolveCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
            if (userIdResult.Error is not null)
            {
                return userIdResult.Error;
            }

            var serviceTarget = $"gui/{userIdResult.UserId}/{label}";
            var bootoutResult = await RunProcessAsync(
                    LaunchctlExecutablePath,
                    ["bootout", serviceTarget],
                    captureStandardOutput: false,
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return IsSuccessfulBootout(bootoutResult)
                ? null
                : CreateProcessError("release supervisor LaunchAgent", bootoutResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ExecutionError.InternalError($"Failed to release supervisor with launchctl. {exception.Message}");
        }
    }

    private async ValueTask<(string? UserId, ExecutionError? Error)> ResolveCurrentUserIdAsync (
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
                CurrentUserIdExecutablePath,
                ["-u"],
                captureStandardOutput: true,
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSuccessfulExit(result))
        {
            return (null, CreateProcessError("resolve current user identifier", result));
        }

        var output = result.StandardOutput?.Trim();
        return string.IsNullOrEmpty(output)
            ? (null, ExecutionError.InternalError("Current user identifier could not be resolved for supervisor LaunchAgent."))
            : (output, null);
    }

    private async ValueTask<ProcessRunResult> RunProcessAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        bool captureStandardOutput,
        CancellationToken cancellationToken)
    {
        return await processRunner.RunAsync(
                new ProcessRunRequest(
                    FileName: fileName,
                    Arguments: arguments,
                    Timeout: SupervisorConstants.ProcessManagerCommandTimeout,
                    CaptureStandardOutput: captureStandardOutput,
                    OutputDrainMode: ProcessOutputDrainMode.WaitForCompletion,
                    TerminationPolicy: ProcessTerminationPolicy.ForceKill),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<SupervisorProcessLaunchResult> CreateFailureAfterRollbackAsync (
        ExecutionError primaryError,
        ISupervisorProcessLaunchLease launchLease)
    {
        var rollbackError = await TryRollbackAsync(launchLease).ConfigureAwait(false);
        if (rollbackError is null)
        {
            return SupervisorProcessLaunchResult.Failure(primaryError);
        }

        return SupervisorProcessLaunchResult.FailureWithLease(
            primaryError with
            {
                Message = $"{primaryError.Message} RegistrationRollback={rollbackError.Message}",
            },
            launchLease);
    }

    private static async ValueTask<ExecutionError?> TryRollbackAsync (ISupervisorProcessLaunchLease launchLease)
    {
        try
        {
            return await launchLease.RollbackAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return ExecutionError.InternalError(
                $"Supervisor LaunchAgent registration rollback crashed. {exception.Message}");
        }
    }

    private async ValueTask<ExecutionError?> RollbackPossibleRegistrationAsync (string serviceTarget)
    {
        try
        {
            var rollbackResult = await RunProcessAsync(
                    LaunchctlExecutablePath,
                    ["bootout", "--wait", serviceTarget],
                    captureStandardOutput: false,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return IsSuccessfulBootout(rollbackResult)
                ? null
                : CreateProcessError("roll back supervisor LaunchAgent registration", rollbackResult);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ExecutionError.InternalError(
                $"Failed to roll back supervisor LaunchAgent registration. {exception.Message}");
        }
    }

    private static bool IsSuccessfulExit (ProcessRunResult result)
    {
        return result.Status == ProcessRunStatus.Exited && result.ExitCode == 0;
    }

    private static bool IsSuccessfulBootout (ProcessRunResult result)
    {
        return result.Status == ProcessRunStatus.Exited
            && result.ExitCode is 0 or 3;
    }

    private static ExecutionError CreateProcessError (
        string operation,
        ProcessRunResult result)
    {
        var message = result.ErrorMessage
            ?? $"Process status={result.Status}, exitCode={result.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable"}.";
        return result.Status == ProcessRunStatus.TimedOut
            ? ExecutionError.Timeout($"Timed out while attempting to {operation}. {message}")
            : ExecutionError.InternalError($"Failed to {operation}. {message}");
    }

    private static string BuildLaunchdLabel (SupervisorWorktreeIdentity worktreeIdentity)
    {
        return "dev.mackysoft.ucli.supervisor." + worktreeIdentity.LaunchServiceNameSuffix;
    }

    private sealed class LaunchdSupervisorProcessLaunchLease : ISupervisorProcessLaunchLease
    {
        private readonly LaunchdSupervisorProcessManager processManager;

        private readonly string serviceTarget;

        private bool finalized;

        public LaunchdSupervisorProcessLaunchLease (
            LaunchdSupervisorProcessManager processManager,
            string serviceTarget)
        {
            this.processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            this.serviceTarget = !string.IsNullOrWhiteSpace(serviceTarget)
                ? serviceTarget
                : throw new ArgumentException("LaunchAgent service target must not be empty.", nameof(serviceTarget));
        }

        public ValueTask CommitAsync ()
        {
            finalized = true;
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ExecutionError?> RollbackAsync ()
        {
            if (finalized)
            {
                return null;
            }

            var error = await processManager.RollbackPossibleRegistrationAsync(serviceTarget).ConfigureAwait(false);
            if (error is null)
            {
                finalized = true;
            }

            return error;
        }
    }
}
