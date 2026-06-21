using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>build.run</c> IPC response payload. </summary>
/// <param name="RunId"> The build run identifier. </param>
/// <param name="ProjectFingerprint"> The project fingerprint served by the Unity IPC host. </param>
/// <param name="LifecycleBefore"> The lifecycle snapshot captured before BuildPipeline execution. </param>
/// <param name="LifecycleAfter"> The lifecycle snapshot captured after BuildPipeline execution. </param>
/// <param name="DirtyState"> The dirty-state precondition probe result. </param>
/// <param name="Input"> The resolved BuildPipeline input. </param>
/// <param name="OutputLayout"> The BuildPipeline output layout used by Unity, or <see langword="null" /> when the runner does not produce BuildPipeline output. </param>
/// <param name="UnityBuildProfile"> The resolved Unity Build Profile input when one was used. </param>
/// <param name="Report"> The normalized BuildReport artifact payload written by Unity. </param>
/// <param name="Logs"> The build log artifact summary. </param>
/// <param name="ProjectMutation"> The project mutation audit captured around runner invocation. </param>
public sealed record IpcBuildRunResponse (
    string RunId,
    string ProjectFingerprint,
    IpcBuildLifecycleSnapshot LifecycleBefore,
    IpcBuildLifecycleSnapshot LifecycleAfter,
    IpcBuildDirtyState DirtyState,
    IpcBuildInputProbe Input,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcBuildOutputLayout? OutputLayout,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcUnityBuildProfileInput? UnityBuildProfile,
    IpcBuildReportArtifact Report,
    IpcBuildLogSummary Logs,
    IpcBuildProjectMutationAudit ProjectMutation)
{
    /// <summary> Gets the normalized runner terminal result when provided by the Unity runtime. </summary>
    public IpcBuildRunnerResultArtifact? RunnerResult { get; init; }
}
