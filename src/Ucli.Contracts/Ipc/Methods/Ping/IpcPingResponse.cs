using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>ping</c> IPC response payload. </summary>
public sealed record IpcPingResponse
{
    /// <summary> Initializes one validated ping response payload. </summary>
    [JsonConstructor]
    public IpcPingResponse (
        string ServerVersion,
        string EditorMode,
        string UnityVersion,
        ProjectFingerprint ProjectFingerprint,
        string? CompileState,
        string? LifecycleState = null,
        string? BlockingReason = null,
        string? CompileGeneration = null,
        string? DomainReloadGeneration = null,
        bool CanAcceptExecutionRequests = false,
        DateTimeOffset? ObservedAtUtc = null,
        string? ActionRequired = null,
        IpcPrimaryDiagnostic? PrimaryDiagnostic = null,
        IpcPlayModeSnapshot? PlayMode = null)
    {
        this.ServerVersion = ContractArgumentGuard.RequireValue(ServerVersion, nameof(ServerVersion));
        this.EditorMode = ContractArgumentGuard.RequireValue(EditorMode, nameof(EditorMode));
        this.UnityVersion = ContractArgumentGuard.RequireValue(UnityVersion, nameof(UnityVersion));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.CompileState = CompileState;
        this.LifecycleState = LifecycleState;
        this.BlockingReason = BlockingReason;
        this.CompileGeneration = CompileGeneration;
        this.DomainReloadGeneration = DomainReloadGeneration;
        this.CanAcceptExecutionRequests = CanAcceptExecutionRequests;
        this.ObservedAtUtc = ObservedAtUtc;
        this.ActionRequired = ActionRequired;
        this.PrimaryDiagnostic = PrimaryDiagnostic;
        this.PlayMode = PlayMode;
    }

    public string ServerVersion { get; }

    public string EditorMode { get; }

    public string UnityVersion { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public string? CompileState { get; }

    public string? LifecycleState { get; }

    public string? BlockingReason { get; }

    public string? CompileGeneration { get; }

    public string? DomainReloadGeneration { get; }

    public bool CanAcceptExecutionRequests { get; }

    public DateTimeOffset? ObservedAtUtc { get; }

    public string? ActionRequired { get; }

    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }

    public IpcPlayModeSnapshot? PlayMode { get; }
}
