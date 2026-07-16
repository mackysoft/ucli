using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Manages the worktree-local supervisor as a detached process owned by the operating system. </summary>
internal sealed class SupervisorProcessManager : ISupervisorProcessManager
{
    private readonly SupervisorLaunchCommandResolver launchCommandResolver;

    private readonly LaunchdSupervisorProcessManager launchdManager;

    private readonly SystemdRunSupervisorProcessLauncher systemdRunLauncher;

    private readonly WindowsDetachedSupervisorProcessLauncher windowsDetachedLauncher;

    /// <summary> Initializes a new instance of the <see cref="SupervisorProcessManager" /> class. </summary>
    /// <param name="launchCommandResolver"> The launch-command resolver dependency. </param>
    /// <param name="launchdManager"> The macOS launchd-based process-manager dependency. </param>
    /// <param name="systemdRunLauncher"> The Linux systemd-run launcher dependency. </param>
    /// <param name="windowsDetachedLauncher"> The Windows detached-process launcher dependency. </param>
    public SupervisorProcessManager (
        SupervisorLaunchCommandResolver launchCommandResolver,
        LaunchdSupervisorProcessManager launchdManager,
        SystemdRunSupervisorProcessLauncher systemdRunLauncher,
        WindowsDetachedSupervisorProcessLauncher windowsDetachedLauncher)
    {
        this.launchCommandResolver = launchCommandResolver ?? throw new ArgumentNullException(nameof(launchCommandResolver));
        this.launchdManager = launchdManager ?? throw new ArgumentNullException(nameof(launchdManager));
        this.systemdRunLauncher = systemdRunLauncher ?? throw new ArgumentNullException(nameof(systemdRunLauncher));
        this.windowsDetachedLauncher = windowsDetachedLauncher ?? throw new ArgumentNullException(nameof(windowsDetachedLauncher));
    }

    /// <summary> Launches the supervisor for the specified storage root. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> LaunchAsync (
        string storageRoot,
        CancellationToken cancellationToken)
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
            return await launchdManager.LaunchAsync(storageRoot, launchCommand, cancellationToken).ConfigureAwait(false);
        }

        if (OperatingSystem.IsLinux())
        {
            return await systemdRunLauncher.LaunchAsync(storageRoot, launchCommand, cancellationToken).ConfigureAwait(false);
        }

        if (OperatingSystem.IsWindows())
        {
            return windowsDetachedLauncher.Launch(storageRoot, launchCommand);
        }

        return ExecutionError.InternalError("Supervisor launch is not supported on the current operating system.");
    }

    /// <summary> Releases the platform registration for the specified storage root after supervisor retirement. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="releaseMode"> Whether release must wait for the registered process to terminate. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by supervisor retirement. </param>
    /// <returns> One structured error when release fails; otherwise <see langword="null" />. </returns>
    public ValueTask<ExecutionError?> ReleaseAsync (
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

        return OperatingSystem.IsMacOS()
            ? launchdManager.ReleaseAsync(storageRoot, releaseMode, cancellationToken)
            : ValueTask.FromResult<ExecutionError?>(null);
    }
}
