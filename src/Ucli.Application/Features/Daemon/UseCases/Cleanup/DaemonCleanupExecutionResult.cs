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

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;

/// <summary> Represents the result of one daemon-cleanup command workflow execution. </summary>
/// <param name="Output"> The daemon-cleanup execution output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured execution error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonCleanupExecutionResult (
    DaemonCleanupExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon-cleanup workflow execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful daemon-cleanup execution result. </summary>
    /// <param name="output"> The normalized daemon-cleanup execution output. </param>
    /// <returns> The successful daemon-cleanup execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static DaemonCleanupExecutionResult Success (DaemonCleanupExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new DaemonCleanupExecutionResult(output, null);
    }

    /// <summary> Creates a failed daemon-cleanup execution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-cleanup execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCleanupExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCleanupExecutionResult(null, error);
    }
}
