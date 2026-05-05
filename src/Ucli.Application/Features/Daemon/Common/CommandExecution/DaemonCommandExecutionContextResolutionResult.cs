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
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

/// <summary> Represents the result of resolving daemon command execution context values. </summary>
/// <param name="Context"> The resolved execution context on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonCommandExecutionContextResolutionResult (
    DaemonCommandExecutionContext? Context,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether execution-context resolution succeeded. </summary>
    public bool IsSuccess => Context is not null && Error is null;

    /// <summary> Creates a successful daemon-command execution-context resolution result. </summary>
    /// <param name="context"> The resolved daemon-command execution context. </param>
    /// <returns> The successful daemon-command execution-context resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public static DaemonCommandExecutionContextResolutionResult Success (DaemonCommandExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new DaemonCommandExecutionContextResolutionResult(context, null);
    }

    /// <summary> Creates a failed daemon-command execution-context resolution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-command execution-context resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCommandExecutionContextResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCommandExecutionContextResolutionResult(null, error);
    }
}
