using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;

/// <summary> Represents the result of daemon start operation. </summary>
internal sealed record DaemonStartResult
{
    private DaemonStartResult (
        DaemonStartStatus status,
        DaemonSession? session,
        ExecutionError? error,
        DaemonDiagnosis? diagnosis,
        DaemonStartupObservation? startup,
        DaemonStatusKind daemonStatus,
        IpcUnityEditorObservation? lifecycleObservation)
    {
        Status = status;
        Session = session;
        Error = error;
        Diagnosis = diagnosis;
        Startup = startup;
        DaemonStatus = daemonStatus;
        LifecycleObservation = lifecycleObservation;
    }

    /// <summary> Gets the daemon start outcome. </summary>
    public DaemonStartStatus Status { get; }

    /// <summary> Gets the daemon session metadata for a successful result; otherwise <see langword="null" />. </summary>
    public DaemonSession? Session { get; }

    /// <summary> Gets the structured error for a failed result; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets the optional failure diagnosis that should be projected with a failed result. </summary>
    public DaemonDiagnosis? Diagnosis { get; }

    /// <summary> Gets the optional endpoint-registration startup observation attached to a failed result. </summary>
    public DaemonStartupObservation? Startup { get; }

    /// <summary> Gets the daemon status after start processing. </summary>
    public DaemonStatusKind DaemonStatus { get; }

    /// <summary> Gets the endpoint-registered lifecycle observation for a successful result; otherwise <see langword="null" />. </summary>
    public IpcUnityEditorObservation? LifecycleObservation { get; }

    /// <summary> Gets a value indicating whether daemon start succeeded, detected an already-running daemon, or attached to an existing GUI daemon. </summary>
    [MemberNotNullWhen(true, nameof(Session), nameof(LifecycleObservation))]
    public bool IsSuccess => Status is DaemonStartStatus.Started
        or DaemonStartStatus.AlreadyRunning
        or DaemonStartStatus.Attached;

    /// <summary> Creates a successful start result. </summary>
    /// <param name="session"> The started daemon session metadata. </param>
    /// <param name="lifecycleObservation"> The endpoint-registered lifecycle observation. </param>
    /// <returns> The successful start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> or <paramref name="lifecycleObservation" /> is <see langword="null" />. </exception>
    public static DaemonStartResult Started (
        DaemonSession session,
        IpcUnityEditorObservation lifecycleObservation)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lifecycleObservation);
        return new DaemonStartResult(
            DaemonStartStatus.Started,
            session,
            null,
            null,
            null,
            DaemonStatusKind.Running,
            lifecycleObservation);
    }

    /// <summary> Creates an already-running result. </summary>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <param name="lifecycleObservation"> The endpoint-registered lifecycle observation. </param>
    /// <returns> The already-running result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> or <paramref name="lifecycleObservation" /> is <see langword="null" />. </exception>
    public static DaemonStartResult AlreadyRunning (
        DaemonSession session,
        IpcUnityEditorObservation lifecycleObservation)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lifecycleObservation);
        return new DaemonStartResult(
            DaemonStartStatus.AlreadyRunning,
            session,
            null,
            null,
            null,
            DaemonStatusKind.Running,
            lifecycleObservation);
    }

    /// <summary> Creates an attached result for a daemon session registered by an existing GUI Editor process. </summary>
    /// <param name="session"> The attached daemon session metadata. </param>
    /// <param name="lifecycleObservation"> The endpoint-registered lifecycle observation. </param>
    /// <returns> The attached result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> or <paramref name="lifecycleObservation" /> is <see langword="null" />. </exception>
    public static DaemonStartResult Attached (
        DaemonSession session,
        IpcUnityEditorObservation lifecycleObservation)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lifecycleObservation);
        return new DaemonStartResult(
            DaemonStartStatus.Attached,
            session,
            null,
            null,
            null,
            DaemonStatusKind.Running,
            lifecycleObservation);
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
        if (!TextVocabulary.IsDefined(daemonStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(daemonStatus), daemonStatus, "Daemon status must have a contract literal.");
        }

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
