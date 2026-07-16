using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Represents the result of daemon status query operation. </summary>
internal sealed record DaemonStatusResult
{
    private DaemonStatusResult (
        DaemonStatusKind? status,
        DaemonSession? session,
        DaemonDiagnosis? diagnosis,
        ExecutionError? error,
        IpcUnityEditorObservation? pingResponse,
        DaemonLaunchAttempt? lastLaunchAttempt)
    {
        Status = status;
        Session = session;
        Diagnosis = diagnosis;
        Error = error;
        PingResponse = pingResponse;
        LastLaunchAttempt = lastLaunchAttempt;
    }

    /// <summary> Gets the daemon status outcome for a successful query; otherwise <see langword="null" />. </summary>
    public DaemonStatusKind? Status { get; }

    /// <summary> Gets the daemon session metadata when available. </summary>
    public DaemonSession? Session { get; }

    /// <summary> Gets the daemon diagnosis metadata when available. </summary>
    public DaemonDiagnosis? Diagnosis { get; }

    /// <summary> Gets the structured error when status query fails. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets the ping payload for a running daemon; otherwise <see langword="null" />. </summary>
    public IpcUnityEditorObservation? PingResponse { get; }

    /// <summary> Gets the most recent session-less launch attempt when available. </summary>
    public DaemonLaunchAttempt? LastLaunchAttempt { get; }

    /// <summary> Gets a value indicating whether daemon status query succeeded. </summary>
    [MemberNotNullWhen(true, nameof(Status))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Status.HasValue;

    /// <summary> Creates a running result. </summary>
    /// <param name="session"> The current daemon session metadata. </param>
    /// <param name="pingResponse"> The ping response observed for the current session. </param>
    /// <param name="diagnosis"> The persisted daemon diagnosis metadata when available. </param>
    /// <returns> The running result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> or <paramref name="pingResponse" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Running (
        DaemonSession session,
        IpcUnityEditorObservation pingResponse,
        DaemonDiagnosis? diagnosis)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(pingResponse);
        return new DaemonStatusResult(
            DaemonStatusKind.Running,
            session,
            diagnosis,
            error: null,
            pingResponse,
            lastLaunchAttempt: null);
    }

    /// <summary> Creates a not-running result. </summary>
    /// <param name="diagnosis"> The persisted daemon diagnosis metadata when available. </param>
    /// <param name="lastLaunchAttempt"> The most recent session-less launch attempt when available. </param>
    /// <returns> The not-running result. </returns>
    public static DaemonStatusResult NotRunning (
        DaemonDiagnosis? diagnosis,
        DaemonLaunchAttempt? lastLaunchAttempt)
    {
        return new DaemonStatusResult(
            status: DaemonStatusKind.NotRunning,
            session: null,
            diagnosis,
            error: null,
            pingResponse: null,
            lastLaunchAttempt);
    }

    /// <summary> Creates a stale-session result. </summary>
    /// <param name="session"> The stale daemon session metadata. </param>
    /// <param name="diagnosis"> The daemon diagnosis metadata when available. </param>
    /// <returns> The stale-session result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Stale (
        DaemonSession session,
        DaemonDiagnosis? diagnosis)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStatusResult(
            DaemonStatusKind.Stale,
            session,
            diagnosis,
            error: null,
            pingResponse: null,
            lastLaunchAttempt: null);
    }

    /// <summary> Creates a failed status-query result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed status-query result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStatusResult(
            status: null,
            session: null,
            diagnosis: null,
            error,
            pingResponse: null,
            lastLaunchAttempt: null);
    }
}
