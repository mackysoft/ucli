using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Represents the outcome of probing one exact daemon-session generation. </summary>
internal sealed record DaemonSessionProbeResult
{
    private DaemonSessionProbeResult (
        DaemonSession session,
        IpcUnityEditorObservation? pingResponse,
        DaemonSessionReadResult? sessionReadFailure,
        Exception? probeFailure)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        PingResponse = pingResponse;
        SessionReadFailure = sessionReadFailure;
        ProbeFailure = probeFailure;
    }

    /// <summary> Gets the exact session generation that responded or produced the probe failure. </summary>
    public DaemonSession Session { get; }

    /// <summary> Gets the decoded ping response on success. </summary>
    public IpcUnityEditorObservation? PingResponse { get; }

    /// <summary> Gets the refreshed-session read failure after token rotation. </summary>
    public DaemonSessionReadResult? SessionReadFailure { get; }

    /// <summary> Gets the failure produced while probing <see cref="Session" />. </summary>
    public Exception? ProbeFailure { get; }

    /// <summary> Gets a value indicating whether an exact session responded. </summary>
    public bool IsSuccess => PingResponse is not null;

    /// <summary> Creates a successful exact-session probe result. </summary>
    public static DaemonSessionProbeResult Success (
        DaemonSession session,
        IpcUnityEditorObservation pingResponse)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(pingResponse);
        return new DaemonSessionProbeResult(
            session,
            pingResponse,
            sessionReadFailure: null,
            probeFailure: null);
    }

    /// <summary> Creates a failed result for an invalid refreshed-session read. </summary>
    public static DaemonSessionProbeResult SessionReadFailed (
        DaemonSession session,
        DaemonSessionReadResult sessionReadFailure)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sessionReadFailure);
        if (sessionReadFailure.IsSuccess)
        {
            throw new ArgumentException(
                "A failed daemon session read result is required.",
                nameof(sessionReadFailure));
        }

        return new DaemonSessionProbeResult(
            session,
            pingResponse: null,
            sessionReadFailure,
            probeFailure: null);
    }

    /// <summary> Creates a failed result for an exception produced while probing one exact session. </summary>
    public static DaemonSessionProbeResult ProbeFailed (
        DaemonSession session,
        Exception probeFailure)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(probeFailure);
        return new DaemonSessionProbeResult(
            session,
            pingResponse: null,
            sessionReadFailure: null,
            probeFailure);
    }
}
