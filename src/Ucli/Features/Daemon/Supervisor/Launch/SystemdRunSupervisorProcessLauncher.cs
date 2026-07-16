using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Launches the worktree-local supervisor through <c>systemd-run --user --collect</c>. </summary>
internal sealed class SystemdRunSupervisorProcessLauncher
{
    private const int UnitNotLoadedExitCode = 5;

    private readonly IProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="SystemdRunSupervisorProcessLauncher" /> class. </summary>
    /// <param name="processRunner"> The external process-runner dependency. </param>
    public SystemdRunSupervisorProcessLauncher (IProcessRunner processRunner)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <summary> Launches the supervisor for the specified storage root by using <c>systemd-run</c>. </summary>
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

        SystemdSupervisorProcessLaunchLease? launchLease = null;
        try
        {
            var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
            var normalizedStorageRoot = worktreeIdentity.NormalizedStorageRoot;
            var unitName = BuildSystemdUnitName(worktreeIdentity);
            var arguments = BuildArguments(normalizedStorageRoot, unitName, launchCommand);
            launchLease = new SystemdSupervisorProcessLaunchLease(this, unitName);

            var launchResult = await processRunner.RunAsync(
                    new ProcessRunRequest(
                        FileName: "systemd-run",
                        Arguments: arguments,
                        Timeout: SupervisorConstants.ProcessManagerCommandTimeout,
                        CaptureStandardOutput: false,
                        OutputDrainMode: ProcessOutputDrainMode.WaitForCompletion,
                        TerminationPolicy: ProcessTerminationPolicy.ForceKill),
                    cancellationToken)
                .ConfigureAwait(false);
            if (launchResult.Status == ProcessRunStatus.Exited && launchResult.ExitCode == 0)
            {
                return SupervisorProcessLaunchResult.Success(launchLease);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var message = launchResult.ErrorMessage ?? $"Process status={launchResult.Status}.";
            var error = launchResult.Status == ProcessRunStatus.TimedOut
                ? ExecutionError.Timeout($"Timed out while launching supervisor with systemd-run. {message}")
                : ExecutionError.InternalError($"Failed to launch supervisor with systemd-run. {message}");
            return launchResult.Status == ProcessRunStatus.StartFailed
                ? SupervisorProcessLaunchResult.Failure(error)
                : await CreateFailureAfterRollbackAsync(error, launchLease).ConfigureAwait(false);
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
                    $"Supervisor systemd launch was canceled. UnitRollback={rollbackError.Message}"),
                launchLease);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var error = ExecutionError.InternalError($"Failed to launch supervisor with systemd-run. {exception.Message}");
            return launchLease is null
                ? SupervisorProcessLaunchResult.Failure(error)
                : await CreateFailureAfterRollbackAsync(error, launchLease).ConfigureAwait(false);
        }
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
                Message = $"{primaryError.Message} UnitRollback={rollbackError.Message}",
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
                $"Supervisor systemd unit rollback crashed. {exception.Message}");
        }
    }

    private async ValueTask<ExecutionError?> StopUnitAsync (string unitName)
    {
        try
        {
            var stopResult = await processRunner.RunAsync(
                    new ProcessRunRequest(
                        FileName: "systemctl",
                        Arguments: ["--user", "stop", unitName],
                        Timeout: SupervisorConstants.ProcessManagerCommandTimeout,
                        CaptureStandardOutput: false,
                        OutputDrainMode: ProcessOutputDrainMode.WaitForCompletion,
                        TerminationPolicy: ProcessTerminationPolicy.ForceKill),
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (stopResult.Status == ProcessRunStatus.Exited
                && stopResult.ExitCode is 0 or UnitNotLoadedExitCode)
            {
                return null;
            }

            var message = stopResult.ErrorMessage ?? $"Process status={stopResult.Status}.";
            return stopResult.Status == ProcessRunStatus.TimedOut
                ? ExecutionError.Timeout($"Timed out while stopping supervisor systemd unit. {message}")
                : ExecutionError.InternalError($"Failed to stop supervisor systemd unit. {message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ExecutionError.InternalError($"Failed to stop supervisor systemd unit. {exception.Message}");
        }
    }

    private static string BuildSystemdUnitName (SupervisorWorktreeIdentity worktreeIdentity)
    {
        return "mackysoft-ucli-supervisor-" + worktreeIdentity.LaunchServiceNameSuffix;
    }

    internal static IReadOnlyList<string> BuildArguments (
        string normalizedStorageRoot,
        string unitName,
        SupervisorLaunchCommand launchCommand)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);

        var arguments = new List<string>
        {
            "--user",
            "--quiet",
            "--collect",
            "--unit",
            unitName,
            "--working-directory",
            normalizedStorageRoot,
            launchCommand.FileName,
        };
        arguments.AddRange(launchCommand.Arguments);
        arguments.AddRange(SupervisorInvocationArguments.Build(normalizedStorageRoot));
        return arguments;
    }

    private sealed class SystemdSupervisorProcessLaunchLease : ISupervisorProcessLaunchLease
    {
        private readonly SystemdRunSupervisorProcessLauncher processLauncher;

        private readonly string unitName;

        private bool finalized;

        public SystemdSupervisorProcessLaunchLease (
            SystemdRunSupervisorProcessLauncher processLauncher,
            string unitName)
        {
            this.processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
            this.unitName = !string.IsNullOrWhiteSpace(unitName)
                ? unitName
                : throw new ArgumentException("systemd unit name must not be empty.", nameof(unitName));
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

            var error = await processLauncher.StopUnitAsync(unitName).ConfigureAwait(false);
            if (error is null)
            {
                finalized = true;
            }

            return error;
        }
    }
}
