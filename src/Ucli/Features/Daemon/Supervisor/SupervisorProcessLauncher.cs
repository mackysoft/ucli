using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor;

/// <summary> Launches the worktree-local supervisor as a detached process owned by the OS process manager. </summary>
internal sealed class SupervisorProcessLauncher : ISupervisorProcessLauncher
{
    private readonly SupervisorLaunchCommandResolver launchCommandResolver;

    private readonly LaunchdSupervisorProcessLauncher launchdLauncher;

    private readonly SystemdRunSupervisorProcessLauncher systemdRunLauncher;

    private readonly WindowsDetachedSupervisorProcessLauncher windowsDetachedLauncher;

    /// <summary> Initializes a new instance of the <see cref="SupervisorProcessLauncher" /> class. </summary>
    /// <param name="launchCommandResolver"> The launch-command resolver dependency. </param>
    /// <param name="launchdLauncher"> The macOS launchd-based launcher dependency. </param>
    /// <param name="systemdRunLauncher"> The Linux systemd-run launcher dependency. </param>
    /// <param name="windowsDetachedLauncher"> The Windows detached-process launcher dependency. </param>
    public SupervisorProcessLauncher (
        SupervisorLaunchCommandResolver launchCommandResolver,
        LaunchdSupervisorProcessLauncher launchdLauncher,
        SystemdRunSupervisorProcessLauncher systemdRunLauncher,
        WindowsDetachedSupervisorProcessLauncher windowsDetachedLauncher)
    {
        this.launchCommandResolver = launchCommandResolver ?? throw new ArgumentNullException(nameof(launchCommandResolver));
        this.launchdLauncher = launchdLauncher ?? throw new ArgumentNullException(nameof(launchdLauncher));
        this.systemdRunLauncher = systemdRunLauncher ?? throw new ArgumentNullException(nameof(systemdRunLauncher));
        this.windowsDetachedLauncher = windowsDetachedLauncher ?? throw new ArgumentNullException(nameof(windowsDetachedLauncher));
    }

    /// <summary> Launches the supervisor for the specified storage root. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> Launch (
        string storageRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var launchCommandResult = launchCommandResolver.Resolve();
        if (!launchCommandResult.IsSuccess)
        {
            return launchCommandResult.Error!;
        }

        var launchCommand = launchCommandResult.Command!;
        if (OperatingSystem.IsMacOS())
        {
            return await launchdLauncher.Launch(storageRoot, launchCommand, cancellationToken).ConfigureAwait(false);
        }

        if (OperatingSystem.IsLinux())
        {
            return await systemdRunLauncher.Launch(storageRoot, launchCommand, cancellationToken).ConfigureAwait(false);
        }

        if (OperatingSystem.IsWindows())
        {
            return windowsDetachedLauncher.Launch(storageRoot, launchCommand);
        }

        return ExecutionError.InternalError("Supervisor launch is not supported on the current operating system.");
    }
}