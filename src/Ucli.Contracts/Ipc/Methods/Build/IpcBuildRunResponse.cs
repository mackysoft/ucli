using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>build.run</c> IPC response payload. </summary>
public sealed record IpcBuildRunResponse
{
    /// <summary> Initializes one validated build-run response payload. </summary>
    [JsonConstructor]
    public IpcBuildRunResponse (
        Guid RunId,
        ProjectFingerprint ProjectFingerprint,
        IpcBuildLifecycleSnapshot LifecycleBefore,
        IpcBuildLifecycleSnapshot LifecycleAfter,
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

    public IpcBuildLifecycleSnapshot LifecycleBefore { get; }

    public IpcBuildLifecycleSnapshot LifecycleAfter { get; }

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
