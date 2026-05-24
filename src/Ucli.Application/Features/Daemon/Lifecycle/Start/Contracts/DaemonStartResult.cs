using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;

/// <summary> Represents the result of daemon start operation. </summary>
/// <param name="Status"> The daemon start outcome. </param>
/// <param name="Session"> The daemon session metadata when start succeeds or daemon is already running. </param>
/// <param name="Error"> The structured error when start fails. </param>
/// <param name="Diagnosis"> The failure diagnosis that should be projected with the start response when available. </param>
/// <param name="Startup"> The endpoint-registration startup observation attached to a failure when available. </param>
/// <param name="DaemonStatus"> The daemon status after start processing. </param>
/// <param name="LifecycleSnapshot"> The endpoint-registered lifecycle snapshot attached to a successful start. </param>
internal sealed record DaemonStartResult (
    DaemonStartStatus Status,
    DaemonSession? Session,
    ExecutionError? Error,
    DaemonDiagnosis? Diagnosis,
    DaemonStartupObservation? Startup,
    DaemonStatusKind DaemonStatus,
    DaemonStartLifecycleSnapshot? LifecycleSnapshot)
{
    /// <summary> Gets a value indicating whether daemon start succeeded, detected an already-running daemon, or attached to an existing GUI daemon. </summary>
    public bool IsSuccess => (Status == DaemonStartStatus.Started || Status == DaemonStartStatus.AlreadyRunning || Status == DaemonStartStatus.Attached)
        && Session is not null
        && Error is null
        && Diagnosis is null;

    /// <summary> Creates a successful start result. </summary>
    /// <param name="session"> The started daemon session metadata. </param>
    /// <param name="lifecycleSnapshot"> The endpoint-registered lifecycle snapshot when available. </param>
    /// <returns> The successful start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStartResult Started (
        DaemonSession session,
        DaemonStartLifecycleSnapshot? lifecycleSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStartResult(
            DaemonStartStatus.Started,
            session,
            null,
            null,
            null,
            DaemonStatusKind.Running,
            lifecycleSnapshot);
    }

    /// <summary> Creates an already-running result. </summary>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <param name="lifecycleSnapshot"> The endpoint-registered lifecycle snapshot when available. </param>
    /// <returns> The already-running result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStartResult AlreadyRunning (
        DaemonSession session,
        DaemonStartLifecycleSnapshot? lifecycleSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStartResult(
            DaemonStartStatus.AlreadyRunning,
            session,
            null,
            null,
            null,
            DaemonStatusKind.Running,
            lifecycleSnapshot);
    }

    /// <summary> Creates an attached result for a daemon session registered by an existing GUI Editor process. </summary>
    /// <param name="session"> The attached daemon session metadata. </param>
    /// <param name="lifecycleSnapshot"> The endpoint-registered lifecycle snapshot when available. </param>
    /// <returns> The attached result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStartResult Attached (
        DaemonSession session,
        DaemonStartLifecycleSnapshot? lifecycleSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStartResult(
            DaemonStartStatus.Attached,
            session,
            null,
            null,
            null,
            DaemonStatusKind.Running,
            lifecycleSnapshot);
    }

    /// <summary> Creates a failed start result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <param name="diagnosis"> The optional failure diagnosis that should be projected to callers. </param>
    /// <param name="startup"> The optional startup observation attached to the failure. </param>
    /// <param name="daemonStatus"> The daemon status after start processing. </param>
    /// <returns> The failed start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStartResult Failure (
        ExecutionError error,
        DaemonDiagnosis? diagnosis = null,
        DaemonStartupObservation? startup = null,
        DaemonStatusKind daemonStatus = DaemonStatusKind.NotRunning)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStartResult(
            DaemonStartStatus.Failed,
            null,
            error,
            diagnosis,
            startup,
            daemonStatus,
            null);
    }
}
