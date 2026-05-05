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
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Represents the result of launching one Unity batchmode daemon process. </summary>
/// <param name="ProcessId"> The launched process identifier when launch succeeds; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error when launch fails; otherwise <see langword="null" />. </param>
internal sealed record UnityDaemonLaunchResult (
    int? ProcessId,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether launch succeeded. </summary>
    public bool IsSuccess => ProcessId is not null && Error is null;

    /// <summary> Creates a successful launch result. </summary>
    /// <param name="processId"> The launched process identifier. </param>
    /// <returns> The successful launch result. </returns>
    public static UnityDaemonLaunchResult Success (int processId)
    {
        return new UnityDaemonLaunchResult(processId, null);
    }

    /// <summary> Creates a failed launch result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed launch result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityDaemonLaunchResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityDaemonLaunchResult(null, error);
    }
}
