using System.ComponentModel;
using System.Diagnostics;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Launches the worktree-local supervisor as one detached Windows process. </summary>
internal sealed class WindowsDetachedSupervisorProcessLauncher
{
    private readonly IDetachedProcessStarter processStarter;

    /// <summary> Initializes a new instance of the <see cref="WindowsDetachedSupervisorProcessLauncher" /> class. </summary>
    /// <param name="processStarter"> The detached-process starter dependency. </param>
    public WindowsDetachedSupervisorProcessLauncher (IDetachedProcessStarter processStarter)
    {
        this.processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
    }

    /// <summary> Launches the supervisor for the specified storage root by using detached process creation. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="launchCommand"> The resolved relaunch command. </param>
    /// <returns> The launch outcome, including any generation lease whose cleanup ownership remains with the caller. </returns>
    public SupervisorProcessLaunchResult Launch (
        string storageRoot,
        SupervisorLaunchCommand launchCommand)
    {
        try
        {
            var processHandle = processStarter.Start(BuildStartInfo(storageRoot, launchCommand));
            if (processHandle is null)
            {
                return SupervisorProcessLaunchResult.Failure(
                    ExecutionError.InternalError("Supervisor detached process could not be started."));
            }

            var lease = new DetachedSupervisorProcessLaunchLease(processHandle);
            return SupervisorProcessLaunchResult.Success(lease);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return SupervisorProcessLaunchResult.Failure(ExecutionError.InternalError(
                $"Failed to launch supervisor detached process. {exception.Message}"));
        }
    }

    internal static ProcessStartInfo BuildStartInfo (
        string storageRoot,
        SupervisorLaunchCommand launchCommand)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);

        var normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);
        var startInfo = new ProcessStartInfo
        {
            FileName = launchCommand.FileName,
            WorkingDirectory = normalizedStorageRoot,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        for (var i = 0; i < launchCommand.Arguments.Count; i++)
        {
            startInfo.ArgumentList.Add(launchCommand.Arguments[i]);
        }

        var supervisorArguments = SupervisorInvocationArguments.Build(normalizedStorageRoot);
        for (var i = 0; i < supervisorArguments.Length; i++)
        {
            startInfo.ArgumentList.Add(supervisorArguments[i]);
        }

        return startInfo;
    }
}
