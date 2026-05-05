using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Resolves the current uCLI execution shape into a reusable supervisor launch command. </summary>
internal sealed class SupervisorLaunchCommandResolver
{
    /// <summary> Resolves the current process into one launch command suitable for detached supervisor startup. </summary>
    /// <returns> The command-resolution result. </returns>
    public SupervisorLaunchCommandResolutionResult Resolve ()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return SupervisorLaunchCommandResolutionResult.Failure(ExecutionError.InternalError(
                "Current process executable path could not be resolved."));
        }

        if (IsDotNetHost(processPath))
        {
            var commandLineArguments = Environment.GetCommandLineArgs();
            if (commandLineArguments.Length == 0 || string.IsNullOrWhiteSpace(commandLineArguments[0]))
            {
                return SupervisorLaunchCommandResolutionResult.Failure(ExecutionError.InternalError(
                    "Current process application path could not be resolved."));
            }

            return SupervisorLaunchCommandResolutionResult.Success(new SupervisorLaunchCommand(
                processPath,
                [Path.GetFullPath(commandLineArguments[0])]));
        }

        return SupervisorLaunchCommandResolutionResult.Success(new SupervisorLaunchCommand(processPath, []));
    }

    private static bool IsDotNetHost (string processPath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(processPath);
        return string.Equals(fileNameWithoutExtension, "dotnet", StringComparison.OrdinalIgnoreCase);
    }
}
