namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents structured <c>build.run</c> error payload values. </summary>
/// <param name="Project"> The Unity project identity attached to the failed probe. </param>
/// <param name="LifecycleBefore"> The lifecycle snapshot captured before BuildPipeline execution. </param>
/// <param name="DirtyState"> The dirty-state precondition probe result when available. </param>
/// <param name="Input"> The resolved input probe when available. </param>
public sealed record IpcBuildRunErrorPayload (
    IpcProjectIdentity? Project,
    IpcBuildLifecycleSnapshot? LifecycleBefore,
    IpcBuildDirtyState? DirtyState,
    IpcBuildInputProbe? Input);
