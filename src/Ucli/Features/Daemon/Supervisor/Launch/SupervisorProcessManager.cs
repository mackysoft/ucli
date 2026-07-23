using MackySoft.FileSystem;
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
    /// <returns> The launch outcome, including any generation lease whose cleanup ownership remains with the caller. </returns>
    public async ValueTask<SupervisorProcessLaunchResult> LaunchAsync (
        AbsolutePath storageRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var launchCommandResult = launchCommandResolver.Resolve();
        if (!launchCommandResult.IsSuccess)
        {
            return SupervisorProcessLaunchResult.Failure(launchCommandResult.Error!);
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

        return SupervisorProcessLaunchResult.Failure(
            ExecutionError.InternalError("Supervisor launch is not supported on the current operating system."));
    }

    /// <summary> Releases the operating-system registration of the currently executing supervisor. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by supervisor retirement. </param>
    /// <returns> One structured error when release fails; otherwise <see langword="null" />. </returns>
    public ValueTask<ExecutionError?> ReleaseCurrentProcessRegistrationAsync (
        AbsolutePath storageRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return OperatingSystem.IsMacOS()
            ? launchdManager.ReleaseCurrentProcessRegistrationAsync(storageRoot, cancellationToken)
            : ValueTask.FromResult<ExecutionError?>(null);
    }
}
