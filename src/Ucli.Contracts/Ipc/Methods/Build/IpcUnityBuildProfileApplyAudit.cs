using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents observations captured while applying a Unity Build Profile asset. </summary>
public sealed record IpcUnityBuildProfileApplyAudit
{
    /// <summary> Initializes observations captured while applying a Unity Build Profile asset. </summary>
    /// <param name="Applied"> Whether the requested profile became the active Unity Build Profile. </param>
    /// <param name="LifecycleBefore"> The lifecycle observation captured before applying the profile. </param>
    /// <param name="LifecycleAfter"> The lifecycle observation captured after applying the profile. </param>
    /// <param name="DirtyStateAfter"> The dirty-state observation captured after applying the profile. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required observation is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcUnityBuildProfileApplyAudit (
        bool Applied,
        IpcUnityEditorObservation LifecycleBefore,
        IpcUnityEditorObservation LifecycleAfter,
        IpcBuildDirtyState DirtyStateAfter)
    {
        this.Applied = Applied;
        this.LifecycleBefore = ContractArgumentGuard.RequireNotNull(LifecycleBefore, nameof(LifecycleBefore));
        this.LifecycleAfter = ContractArgumentGuard.RequireNotNull(LifecycleAfter, nameof(LifecycleAfter));
        this.DirtyStateAfter = ContractArgumentGuard.RequireNotNull(DirtyStateAfter, nameof(DirtyStateAfter));
    }

    public bool Applied { get; }

    public IpcUnityEditorObservation LifecycleBefore { get; }

    public IpcUnityEditorObservation LifecycleAfter { get; }

    public IpcBuildDirtyState DirtyStateAfter { get; }
}
