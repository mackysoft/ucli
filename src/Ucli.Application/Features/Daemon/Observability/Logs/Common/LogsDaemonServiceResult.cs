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
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;

/// <summary> Represents one <c>logs daemon</c> service execution result. </summary>
/// <param name="Error"> The structured execution error when command failed; otherwise <see langword="null" />. </param>
internal sealed record LogsDaemonServiceResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether execution succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful service result. </summary>
    /// <returns> The successful service result. </returns>
    public static LogsDaemonServiceResult Success ()
    {
        return new LogsDaemonServiceResult(Error: null);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed service result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static LogsDaemonServiceResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LogsDaemonServiceResult(error);
    }
}
