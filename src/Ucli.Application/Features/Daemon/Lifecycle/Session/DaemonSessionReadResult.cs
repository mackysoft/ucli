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

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents the result of reading daemon session metadata from local storage. </summary>
/// <param name="Session"> The loaded daemon session. </param>
/// <param name="Error"> The structured error when read fails; otherwise <see langword="null" />. </param>
/// <param name="FailureKind"> The categorized failure kind when read fails; otherwise <see cref="DaemonSessionReadFailureKind.None" />. </param>
internal sealed record DaemonSessionReadResult (
    DaemonSession? Session,
    ExecutionError? Error,
    DaemonSessionReadFailureKind FailureKind)
{
    /// <summary> Gets a value indicating whether session read operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Gets a value indicating whether a daemon session exists. </summary>
    public bool Exists => IsSuccess && Session is not null;

    /// <summary> Creates a successful read result. </summary>
    /// <param name="session"> The loaded daemon session when one exists; otherwise <see langword="null" />. </param>
    /// <returns> The successful read result. </returns>
    public static DaemonSessionReadResult Success (DaemonSession? session)
    {
        return new DaemonSessionReadResult(session, null, DaemonSessionReadFailureKind.None);
    }

    /// <summary> Creates a failed read result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <param name="failureKind"> The categorized failure kind for read operation. </param>
    /// <param name="session"> The parsed daemon session snapshot when available for cleanup; otherwise <see langword="null" />. </param>
    /// <returns> The failed read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonSessionReadResult Failure (
        ExecutionError error,
        DaemonSessionReadFailureKind failureKind = DaemonSessionReadFailureKind.Unknown,
        DaemonSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonSessionReadResult(session, error, failureKind);
    }
}
