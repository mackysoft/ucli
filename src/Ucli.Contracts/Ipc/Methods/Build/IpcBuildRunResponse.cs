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
/// <param name="Report"> The normalized BuildReport artifact payload written by Unity, or <see langword="null" /> when an executeMethod runner did not provide BuildReport evidence. </param>
/// <param name="Logs"> The build log artifact summary. </param>
/// <param name="ProjectMutation"> The project mutation audit captured around runner invocation. </param>
public sealed record IpcBuildRunResponse
{
    /// <summary> Initializes one validated build-run response payload. </summary>
    [JsonConstructor]
    public IpcBuildRunResponse (
        Guid RunId,
        ProjectFingerprint ProjectFingerprint,
        IpcUnityEditorObservation LifecycleBefore,
        IpcUnityEditorObservation LifecycleAfter,
        IpcBuildDirtyState DirtyState,
        IpcBuildInputProbe Input,
        IpcBuildOutputLayout? OutputLayout,
        IpcUnityBuildProfileInput? UnityBuildProfile,
        IpcBuildReportArtifact? Report,
        IpcBuildLogSummary Logs,
        IpcBuildProjectMutationAudit ProjectMutation)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.LifecycleBefore = ContractArgumentGuard.RequireNotNull(LifecycleBefore, nameof(LifecycleBefore));
        this.LifecycleAfter = ContractArgumentGuard.RequireNotNull(LifecycleAfter, nameof(LifecycleAfter));
        this.DirtyState = ContractArgumentGuard.RequireNotNull(DirtyState, nameof(DirtyState));
        this.Input = ContractArgumentGuard.RequireNotNull(Input, nameof(Input));
        this.OutputLayout = OutputLayout;
        this.UnityBuildProfile = UnityBuildProfile;
        this.Report = Report;
        this.Logs = ContractArgumentGuard.RequireNotNull(Logs, nameof(Logs));
        this.ProjectMutation = ContractArgumentGuard.RequireNotNull(ProjectMutation, nameof(ProjectMutation));
    }

    public Guid RunId { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public IpcUnityEditorObservation LifecycleBefore { get; }

    public IpcUnityEditorObservation LifecycleAfter { get; }

    public IpcBuildDirtyState DirtyState { get; }

    public IpcBuildInputProbe Input { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcBuildOutputLayout? OutputLayout { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcUnityBuildProfileInput? UnityBuildProfile { get; }

    public IpcBuildReportArtifact? Report { get; }

    public IpcBuildLogSummary Logs { get; }

    public IpcBuildProjectMutationAudit ProjectMutation { get; }

    /// <summary> Gets the normalized runner terminal result when provided by the Unity runtime. </summary>
    public IpcBuildRunnerResultArtifact? RunnerResult { get; init; }
}
