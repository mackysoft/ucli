using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>gui.rebootstrap</c> IPC request payload. </summary>
public sealed record IpcGuiRebootstrapRequest
{
    /// <summary> Initializes one validated GUI rebootstrap request payload. </summary>
    [JsonConstructor]
    public IpcGuiRebootstrapRequest (
        ProjectFingerprint ProjectFingerprint,
        bool ReplaceExistingSession)
    {
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.ReplaceExistingSession = ReplaceExistingSession;
    }

    public ProjectFingerprint ProjectFingerprint { get; }

    public bool ReplaceExistingSession { get; }
}
