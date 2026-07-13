namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents observations captured while applying a Unity Build Profile asset. </summary>
/// <param name="Applied"> Whether the requested profile was applied as the active Unity Build Profile. </param>
/// <param name="LifecycleBefore"> The lifecycle snapshot captured before applying the profile. </param>
/// <param name="LifecycleAfter"> The lifecycle snapshot captured after applying the profile. </param>
/// <param name="DirtyStateAfter"> The dirty-state snapshot captured after applying the profile. </param>
public sealed record IpcUnityBuildProfileApplyAudit (
    bool Applied,
    IpcUnityEditorObservation LifecycleBefore,
    IpcUnityEditorObservation LifecycleAfter,
    IpcBuildDirtyState DirtyStateAfter);
