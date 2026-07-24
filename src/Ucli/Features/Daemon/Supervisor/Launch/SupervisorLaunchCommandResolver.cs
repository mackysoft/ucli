using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Resolves the current uCLI execution shape into a reusable supervisor launch command. </summary>
internal sealed class SupervisorLaunchCommandResolver
{
    /// <summary> Resolves the current process into one launch command suitable for detached supervisor startup. </summary>
    /// <returns> The command-resolution result. </returns>
    public SupervisorLaunchCommandResolutionResult Resolve ()
    {
        var processPath = Environment.ProcessPath;
        if (!AbsolutePath.TryParse(processPath, out var executablePath, out var executablePathFailure))
        {
            return SupervisorLaunchCommandResolutionResult.Failure(ExecutionError.InternalError(
                $"Current process executable path is invalid. {executablePathFailure.Message}"));
        }

        if (IsDotNetHost(executablePath))
        {
            var commandLineArguments = Environment.GetCommandLineArgs();
            if (commandLineArguments.Length == 0)
            {
                return SupervisorLaunchCommandResolutionResult.Failure(ExecutionError.InternalError(
                    "Current process application path could not be resolved."));
            }

            if (!AbsolutePath.TryResolve(
                    AbsolutePath.Parse(Environment.CurrentDirectory),
                    commandLineArguments[0],
                    out var applicationPath,
                    out var applicationPathFailure))
            {
                return SupervisorLaunchCommandResolutionResult.Failure(ExecutionError.InternalError(
                    $"Current process application path is invalid. {applicationPathFailure.Message}"));
            }

            return SupervisorLaunchCommandResolutionResult.Success(new SupervisorLaunchCommand(
                executablePath.Value,
                [applicationPath.Value]));
        }

        return SupervisorLaunchCommandResolutionResult.Success(new SupervisorLaunchCommand(executablePath.Value, []));
    }

    private static bool IsDotNetHost (AbsolutePath processPath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(processPath.Value);
        return string.Equals(fileNameWithoutExtension, "dotnet", StringComparison.OrdinalIgnoreCase);
    }
}
