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

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents the result of one daemon session storage write or delete operation. </summary>
/// <param name="Error"> The structured error when operation fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionStoreOperationResult (ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful operation result. </summary>
    /// <returns> The successful operation result. </returns>
    public static DaemonSessionStoreOperationResult Success ()
    {
        return new DaemonSessionStoreOperationResult((ExecutionError?)null);
    }

    /// <summary> Creates a failed operation result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonSessionStoreOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonSessionStoreOperationResult(error);
    }
}
