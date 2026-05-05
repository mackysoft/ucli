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

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

/// <summary> Represents the result of one daemon-list command workflow execution. </summary>
/// <param name="Output"> The daemon-list execution output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured execution error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonListExecutionResult (
    DaemonListExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon-list workflow execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful daemon-list execution result. </summary>
    /// <param name="output"> The normalized daemon-list execution output. </param>
    /// <returns> The successful daemon-list execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static DaemonListExecutionResult Success (DaemonListExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new DaemonListExecutionResult(output, null);
    }

    /// <summary> Creates a failed daemon-list execution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-list execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonListExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonListExecutionResult(null, error);
    }
}
