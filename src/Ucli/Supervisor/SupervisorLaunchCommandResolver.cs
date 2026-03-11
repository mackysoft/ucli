using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Supervisor;

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