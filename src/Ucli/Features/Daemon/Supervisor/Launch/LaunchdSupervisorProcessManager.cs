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
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> LaunchAsync (
        string storageRoot,
        SupervisorLaunchCommand launchCommand,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(launchCommand);

        string? bootstrapServiceTarget = null;
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
                return userIdResult.Error;
            }

            var userDomain = $"gui/{userIdResult.UserId}";
            var serviceTarget = $"{userDomain}/{label}";
            var bootoutResult = await RunProcessAsync(
                    LaunchctlExecutablePath,
                    ["bootout", "--wait", serviceTarget],
                    captureStandardOutput: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!IsSuccessfulBootout(bootoutResult))
            {
                return CreateProcessError("remove stale supervisor LaunchAgent", bootoutResult);
            }

            var plistDirectoryPath = Path.GetDirectoryName(plistPath);
            if (!string.IsNullOrWhiteSpace(plistDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(plistDirectoryPath);
            }

            var plistContents = LaunchAgentPlistDocumentFactory.Build(label, launchCommand, normalizedStorageRoot, logPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(plistPath, plistContents + Environment.NewLine, cancellationToken).ConfigureAwait(false);

            bootstrapServiceTarget = serviceTarget;
            var bootstrapResult = await RunProcessAsync(
                    LaunchctlExecutablePath,
                    ["bootstrap", userDomain, plistPath],
                    captureStandardOutput: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!IsSuccessfulExit(bootstrapResult))
            {
                return await AddRollbackFailureAsync(
                        CreateProcessError("bootstrap supervisor LaunchAgent", bootstrapResult),
                        bootstrapServiceTarget)
                    .ConfigureAwait(false);
            }

            bootstrapServiceTarget = null;
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (bootstrapServiceTarget is not null)
            {
                _ = await RollbackPossibleRegistrationAsync(bootstrapServiceTarget).ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var launchError = ExecutionError.InternalError($"Failed to launch supervisor with launchctl. {exception.Message}");
            return bootstrapServiceTarget is null
                ? launchError
                : await AddRollbackFailureAsync(launchError, bootstrapServiceTarget).ConfigureAwait(false);
        }
    }

    /// <summary> Removes the worktree-local LaunchAgent registration after its supervisor retires. </summary>
    /// <param name="storageRoot"> The storage-root path that deterministically identifies the LaunchAgent. </param>
    /// <param name="releaseMode"> Whether release must wait for the registered process to terminate. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by supervisor retirement. </param>
    /// <returns> One structured error when release fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> ReleaseAsync (
        string storageRoot,
        SupervisorProcessReleaseMode releaseMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (releaseMode is not SupervisorProcessReleaseMode.AwaitTermination
            and not SupervisorProcessReleaseMode.CurrentProcess)
        {
            throw new ArgumentOutOfRangeException(nameof(releaseMode), releaseMode, "Unknown supervisor process-release mode.");
        }

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
            var bootoutArguments = releaseMode switch
            {
                SupervisorProcessReleaseMode.AwaitTermination => new[] { "bootout", "--wait", serviceTarget },
                SupervisorProcessReleaseMode.CurrentProcess => ["bootout", serviceTarget],
                _ => throw new ArgumentOutOfRangeException(nameof(releaseMode), releaseMode, "Unknown supervisor process-release mode."),
            };
            var bootoutResult = await RunProcessAsync(
                    LaunchctlExecutablePath,
                    bootoutArguments,
                    captureStandardOutput: false,
                    cancellationToken)
                .ConfigureAwait(false);
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
        var result = await processRunner.RunAsync(
                new ProcessRunRequest(
                    FileName: fileName,
                    Arguments: arguments,
                    Timeout: SupervisorConstants.ProcessManagerCommandTimeout,
                    CaptureStandardOutput: captureStandardOutput,
                    OutputDrainMode: ProcessOutputDrainMode.WaitForCompletion,
                    TerminationPolicy: ProcessTerminationPolicy.ForceKill),
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async ValueTask<ExecutionError> AddRollbackFailureAsync (
        ExecutionError primaryError,
        string serviceTarget)
    {
        var rollbackError = await RollbackPossibleRegistrationAsync(serviceTarget).ConfigureAwait(false);
        return rollbackError is null
            ? primaryError
            : primaryError with
            {
                Message = $"{primaryError.Message} RegistrationRollback={rollbackError.Message}",
            };
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
}
