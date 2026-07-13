using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>gui.rebootstrap</c> IPC response payload. </summary>
public sealed record IpcGuiRebootstrapResponse
{
    /// <summary> Initializes one validated GUI rebootstrap response payload. </summary>
    [JsonConstructor]
    public IpcGuiRebootstrapResponse (
        bool Accepted,
        ProjectFingerprint ProjectFingerprint,
        int ProcessId)
    {
        this.Accepted = Accepted;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.ProcessId = ContractArgumentGuard.RequirePositive(ProcessId, nameof(ProcessId));
    }

    public bool Accepted { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int ProcessId { get; }
}
