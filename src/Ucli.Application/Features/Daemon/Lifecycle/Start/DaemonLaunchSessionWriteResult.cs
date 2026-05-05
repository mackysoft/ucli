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

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Represents a daemon launch-session persistence result. </summary>
/// <param name="Session"> The persisted daemon session on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonLaunchSessionWriteResult (
    DaemonSession? Session,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether launch-session persistence succeeded. </summary>
    public bool IsSuccess => Session is not null && Error is null;

    /// <summary> Creates a successful launch-session persistence result. </summary>
    /// <param name="session"> The persisted daemon session. </param>
    /// <returns> The successful launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonLaunchSessionWriteResult Success (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonLaunchSessionWriteResult(session, null);
    }

    /// <summary> Creates a failed launch-session persistence result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonLaunchSessionWriteResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLaunchSessionWriteResult(null, error);
    }
}
