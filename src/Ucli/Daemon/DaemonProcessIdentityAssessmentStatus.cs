namespace MackySoft.Ucli.Daemon;

/// <summary> Defines process identity assessment states for daemon-session cleanup. </summary>
internal enum DaemonProcessIdentityAssessmentStatus
{
    /// <summary> Indicates the target process is not running. </summary>
    NotRunning = 0,

    /// <summary> Indicates the target process is running and matches expected daemon identity. </summary>
    MatchingLiveProcess = 1,

    /// <summary> Indicates the target process is live but does not match the expected daemon identity. </summary>
    DifferentProcess = 2,

    /// <summary> Indicates the target process identity could not be assessed. </summary>
    Uncertain = 3,
}