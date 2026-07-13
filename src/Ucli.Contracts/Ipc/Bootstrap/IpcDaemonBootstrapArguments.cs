using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity daemon bootstrap argument payload. </summary>
public sealed record IpcDaemonBootstrapArguments : IpcBatchmodeBootstrapArguments
{
    /// <summary> Initializes one validated Unity daemon bootstrap argument payload. </summary>
    [JsonConstructor]
    public IpcDaemonBootstrapArguments (
        string RepositoryRoot,
        ProjectFingerprint ProjectFingerprint,
        string SessionPath,
        DateTimeOffset SessionIssuedAtUtc,
        string EndpointTransportKind,
        string EndpointAddress)
    {
        this.RepositoryRoot = ContractArgumentGuard.RequireValue(RepositoryRoot, nameof(RepositoryRoot));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.SessionPath = ContractArgumentGuard.RequireValue(SessionPath, nameof(SessionPath));
        this.SessionIssuedAtUtc = SessionIssuedAtUtc;
        this.EndpointTransportKind = ContractArgumentGuard.RequireValue(EndpointTransportKind, nameof(EndpointTransportKind));
        this.EndpointAddress = ContractArgumentGuard.RequireValue(EndpointAddress, nameof(EndpointAddress));
    }

    /// <summary> Gets the non-empty repository root path. </summary>
    public string RepositoryRoot { get; }

    /// <summary> Gets the project fingerprint. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the non-empty daemon session file path. </summary>
    public string SessionPath { get; }

    /// <summary> Gets the daemon session issuance timestamp in UTC. </summary>
    public DateTimeOffset SessionIssuedAtUtc { get; }

    /// <summary> Gets the non-empty endpoint transport kind literal. </summary>
    public string EndpointTransportKind { get; }

    /// <summary> Gets the non-empty endpoint address. </summary>
    public string EndpointAddress { get; }
}
