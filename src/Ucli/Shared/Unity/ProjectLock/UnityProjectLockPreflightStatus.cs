namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Defines the outcome of preparing a Unity project lock file before Unity process startup. </summary>
internal enum UnityProjectLockPreflightStatus
{
    /// <summary> Indicates no Unity lock file is present. </summary>
    Unlocked = 0,

    /// <summary> Indicates the lock file is owned by a live Unity process for the target project. </summary>
    ActiveLock = 1,

    /// <summary> Indicates the lock file was stale and has been removed. </summary>
    StaleLockCleared = 2,

    /// <summary> Indicates the lock file exists, but ownership could not be decided safely. </summary>
    Ambiguous = 3,

    /// <summary> Indicates lock-file inspection failed before ownership could be evaluated. </summary>
    InspectionFailed = 4,

    /// <summary> Indicates the lock file was stale, but cleanup failed. </summary>
    CleanupFailed = 5,
}
