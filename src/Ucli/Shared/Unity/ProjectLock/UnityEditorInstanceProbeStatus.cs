namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Defines the state of Unity's project-local EditorInstance marker. </summary>
internal enum UnityEditorInstanceProbeStatus
{
    /// <summary> Indicates the marker is not present. </summary>
    NotFound = 0,

    /// <summary> Indicates the marker points to a live process. </summary>
    Active = 1,

    /// <summary> Indicates the marker exists but points to no live process. </summary>
    Stale = 2,

    /// <summary> Indicates marker state cannot be decided safely. </summary>
    Ambiguous = 3,
}
