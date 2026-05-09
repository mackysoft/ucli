namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Defines verification results for one Unity GUI Editor process marker candidate. </summary>
internal enum UnityGuiEditorProcessProbeStatus
{
    /// <summary> The recorded process is a live GUI Unity Editor owned by the current user. </summary>
    MatchingGuiEditor = 0,

    /// <summary> The recorded process is not running. </summary>
    NotRunning = 1,

    /// <summary> The recorded process is not owned by the current user. </summary>
    DifferentUser = 2,

    /// <summary> The recorded process appears to be a batchmode Unity process. </summary>
    Batchmode = 3,

    /// <summary> The recorded process does not appear to be a Unity Editor process. </summary>
    NotUnityEditor = 4,

    /// <summary> The marker appears stale because the process started after the marker was updated. </summary>
    StaleMarker = 5,

    /// <summary> The process identity could not be verified. </summary>
    Uncertain = 6,
}
