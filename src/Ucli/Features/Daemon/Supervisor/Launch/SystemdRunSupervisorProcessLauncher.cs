using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Launches the worktree-local supervisor through <c>systemd-run --user --collect</c>. </summary>
internal sealed class SystemdRunSupervisorProcessLauncher
{
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
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> LaunchAsync (
        string storageRoot,
        SupervisorLaunchCommand launchCommand,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(launchCommand);

        try
        {
            var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
            var normalizedStorageRoot = worktreeIdentity.NormalizedStorageRoot;
            var unitName = BuildSystemdUnitName(worktreeIdentity);
            var arguments = BuildArguments(normalizedStorageRoot, unitName, launchCommand);

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
            cancellationToken.ThrowIfCancellationRequested();
            if (launchResult.Status == ProcessRunStatus.Exited && launchResult.ExitCode == 0)
            {
                return null;
            }

            var message = launchResult.ErrorMessage ?? $"Process status={launchResult.Status}.";
            return launchResult.Status == ProcessRunStatus.TimedOut
                ? ExecutionError.Timeout($"Timed out while launching supervisor with systemd-run. {message}")
                : ExecutionError.InternalError($"Failed to launch supervisor with systemd-run. {message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ExecutionError.InternalError($"Failed to launch supervisor with systemd-run. {exception.Message}");
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
}
