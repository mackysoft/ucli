using System.ComponentModel;
using System.Diagnostics;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Launches the worktree-local supervisor as one detached Windows process. </summary>
internal sealed class WindowsDetachedSupervisorProcessLauncher
{
    /// <summary> Launches the supervisor for the specified storage root by using detached process creation. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="launchCommand"> The resolved relaunch command. </param>
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    public ExecutionError? Launch (
        string storageRoot,
        SupervisorLaunchCommand launchCommand)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = launchCommand.FileName,
                WorkingDirectory = Path.GetFullPath(storageRoot),
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            for (var i = 0; i < launchCommand.Arguments.Count; i++)
            {
                startInfo.ArgumentList.Add(launchCommand.Arguments[i]);
            }

            startInfo.ArgumentList.Add(SupervisorConstants.InternalServeFlag);
            startInfo.ArgumentList.Add(SupervisorConstants.RepositoryRootOption);
            startInfo.ArgumentList.Add(Path.GetFullPath(storageRoot));

            var process = Process.Start(startInfo);
            if (process == null)
            {
                return ExecutionError.InternalError("Supervisor detached process could not be started.");
            }

            process.Dispose();
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return ExecutionError.InternalError($"Failed to launch supervisor detached process. {exception.Message}");
        }
    }
}
