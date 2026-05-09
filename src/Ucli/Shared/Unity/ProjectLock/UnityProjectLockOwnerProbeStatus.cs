namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Defines the ownership status for an existing Unity project lock file. </summary>
internal enum UnityProjectLockOwnerProbeStatus
{
    /// <summary> Indicates no live owner process was found. </summary>
    NoOwner = 0,

    /// <summary> Indicates a live owner process for the target project was found. </summary>
    ActiveOwner = 1,

    /// <summary> Indicates ownership could not be determined safely. </summary>
    Ambiguous = 2,
}
