using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity oneshot bootstrap argument payload. </summary>
public sealed record IpcOneshotBootstrapArguments : IpcBatchmodeBootstrapArguments
{
    /// <summary> Initializes one validated Unity oneshot bootstrap argument payload. </summary>
    [JsonConstructor]
    public IpcOneshotBootstrapArguments (
        int ParentProcessId,
        ProjectFingerprint ProjectFingerprint,
        string SessionToken,
        DateTimeOffset ExitDeadlineUtc,
        string EndpointTransportKind,
        string EndpointAddress)
    {
        this.ParentProcessId = ContractArgumentGuard.RequirePositive(ParentProcessId, nameof(ParentProcessId));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.SessionToken = ContractArgumentGuard.RequireValue(SessionToken, nameof(SessionToken));
        this.ExitDeadlineUtc = ExitDeadlineUtc;
        this.EndpointTransportKind = ContractArgumentGuard.RequireValue(EndpointTransportKind, nameof(EndpointTransportKind));
        this.EndpointAddress = ContractArgumentGuard.RequireValue(EndpointAddress, nameof(EndpointAddress));
    }

    /// <summary> Gets the positive originating CLI process identifier. </summary>
    public int ParentProcessId { get; }

    /// <summary> Gets the project fingerprint. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the non-empty dedicated oneshot session token. </summary>
    public string SessionToken { get; }

    /// <summary> Gets the absolute UTC deadline after which the oneshot host must exit itself. </summary>
    public DateTimeOffset ExitDeadlineUtc { get; }

    /// <summary> Gets the non-empty endpoint transport kind literal. </summary>
    public string EndpointTransportKind { get; }

    /// <summary> Gets the non-empty endpoint address. </summary>
    public string EndpointAddress { get; }
}
