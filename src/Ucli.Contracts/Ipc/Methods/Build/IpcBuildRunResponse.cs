using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>build.run</c> IPC response payload. </summary>
public sealed record IpcBuildRunResponse
{
    /// <summary> Initializes one validated build-run response payload. </summary>
    /// <param name="RunId"> The non-empty build run identifier. </param>
    /// <param name="ProjectFingerprint"> The project fingerprint served by the Unity IPC host. </param>
    /// <param name="LifecycleBefore"> The lifecycle snapshot captured before runner invocation. </param>
    /// <param name="LifecycleAfter"> The lifecycle snapshot captured after runner invocation. </param>
    /// <param name="DirtyState"> The dirty-state precondition result. </param>
    /// <param name="Input"> The resolved build input. </param>
    /// <param name="OutputLayout"> The BuildPipeline output layout, or <see langword="null" /> for a runner without BuildPipeline output. </param>
    /// <param name="UnityBuildProfile"> The resolved Unity Build Profile input, or <see langword="null" /> when unused. </param>
    /// <param name="Report"> The normalized BuildReport, or <see langword="null" /> when no BuildReport evidence was produced. </param>
    /// <param name="Logs"> The normalized build log summary. </param>
    /// <param name="ProjectMutation"> The project mutation audit captured around runner invocation. </param>
    /// <param name="RunnerResult"> The normalized runner result, or <see langword="null" /> when the runtime did not provide one. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
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
        IpcBuildProjectMutationAudit ProjectMutation,
        IpcBuildRunnerResultArtifact? RunnerResult)
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
        this.RunnerResult = RunnerResult;
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
    public IpcBuildRunnerResultArtifact? RunnerResult { get; }
}
