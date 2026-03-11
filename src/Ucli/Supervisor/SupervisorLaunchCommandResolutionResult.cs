using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Supervisor;

/// <summary> Represents one supervisor launch-command resolution result. </summary>
internal sealed record SupervisorLaunchCommandResolutionResult (
    SupervisorLaunchCommand? Command,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether launch-command resolution succeeded. </summary>
    public bool IsSuccess => Command is not null && Error is null;

    /// <summary> Creates one successful launch-command resolution result. </summary>
    /// <param name="command"> The resolved launch command. </param>
    /// <returns> The successful result. </returns>
    public static SupervisorLaunchCommandResolutionResult Success (SupervisorLaunchCommand command)
    {
        return new SupervisorLaunchCommandResolutionResult(command, null);
    }

    /// <summary> Creates one failed launch-command resolution result. </summary>
    /// <param name="error"> The structured resolution error. </param>
    /// <returns> The failed result. </returns>
    public static SupervisorLaunchCommandResolutionResult Failure (ExecutionError error)
    {
        return new SupervisorLaunchCommandResolutionResult(null, error);
    }
}