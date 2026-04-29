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
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Status;

/// <summary> Represents the result of daemon status query operation. </summary>
/// <param name="Status"> The daemon status outcome. </param>
/// <param name="Session"> The daemon session metadata when available. </param>
/// <param name="Diagnosis"> The daemon diagnosis metadata when available. </param>
/// <param name="Error"> The structured error when status query fails. </param>
internal sealed record DaemonStatusResult (
    DaemonStatusKind Status,
    DaemonSession? Session,
    DaemonDiagnosis? Diagnosis,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon status query succeeded. </summary>
    public bool IsSuccess => Status != DaemonStatusKind.Failed && Error is null;

    /// <summary> Creates a running result. </summary>
    /// <param name="session"> The current daemon session metadata. </param>
    /// <param name="diagnosis"> The persisted daemon diagnosis metadata when available. </param>
    /// <returns> The running result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Running (
        DaemonSession session,
        DaemonDiagnosis? diagnosis = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStatusResult(DaemonStatusKind.Running, session, diagnosis, null);
    }

    /// <summary> Creates a not-running result. </summary>
    /// <param name="diagnosis"> The persisted daemon diagnosis metadata when available. </param>
    /// <returns> The not-running result. </returns>
    public static DaemonStatusResult NotRunning (DaemonDiagnosis? diagnosis = null)
    {
        return new DaemonStatusResult(DaemonStatusKind.NotRunning, null, diagnosis, null);
    }

    /// <summary> Creates a stale-session result. </summary>
    /// <param name="session"> The stale daemon session metadata. </param>
    /// <param name="diagnosis"> The daemon diagnosis metadata when available. </param>
    /// <returns> The stale-session result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Stale (
        DaemonSession session,
        DaemonDiagnosis? diagnosis = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStatusResult(DaemonStatusKind.Stale, session, diagnosis, null);
    }

    /// <summary> Creates a failed status-query result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed status-query result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStatusResult(DaemonStatusKind.Failed, null, null, error);
    }
}
