namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>build.run</c> IPC response payload. </summary>
/// <param name="RunId"> The build run identifier. </param>
/// <param name="ProjectFingerprint"> The project fingerprint served by the Unity IPC host. </param>
/// <param name="LifecycleBefore"> The lifecycle snapshot captured before BuildPipeline execution. </param>
/// <param name="LifecycleAfter"> The lifecycle snapshot captured after BuildPipeline execution. </param>
/// <param name="DirtyState"> The dirty-state precondition probe result. </param>
/// <param name="Input"> The resolved BuildPipeline input. </param>
/// <param name="Report"> The normalized BuildReport artifact payload written by Unity, or <see langword="null" /> when an executeMethod runner did not provide BuildReport evidence. </param>
/// <param name="Logs"> The build log artifact summary. </param>
/// <param name="ProjectMutation"> The project mutation audit captured around runner invocation. </param>
public sealed record IpcBuildRunResponse (
    string RunId,
    string ProjectFingerprint,
    IpcBuildLifecycleSnapshot LifecycleBefore,
    IpcBuildLifecycleSnapshot LifecycleAfter,
    IpcBuildDirtyState DirtyState,
    IpcBuildInputProbe Input,
    IpcBuildReportArtifact? Report,
    IpcBuildLogSummary Logs,
    IpcBuildProjectMutationAudit ProjectMutation)
{
    /// <summary> Gets the normalized runner terminal result when provided by the Unity runtime. </summary>
    public IpcBuildRunnerResultArtifact? RunnerResult { get; init; }
}
