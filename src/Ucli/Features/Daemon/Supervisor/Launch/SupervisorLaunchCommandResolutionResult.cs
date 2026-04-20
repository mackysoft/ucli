using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

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