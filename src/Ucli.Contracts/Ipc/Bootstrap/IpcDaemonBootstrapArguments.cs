using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity daemon bootstrap argument payload. </summary>
public sealed record IpcDaemonBootstrapArguments : IpcBatchmodeBootstrapArguments
{
    /// <summary> Initializes one validated Unity daemon bootstrap argument payload. </summary>
    /// <param name="RepositoryRoot"> The non-empty repository root path. </param>
    /// <param name="ProjectFingerprint"> The project fingerprint served by the daemon. </param>
    /// <param name="SessionPath"> The non-empty canonical daemon session path. </param>
    /// <param name="SessionGenerationId"> The non-empty identity of the session generation authorized to bootstrap. </param>
    /// <param name="SessionIssuedAtUtc"> The non-default session issuance timestamp with a zero UTC offset. </param>
    /// <param name="Endpoint"> The validated IPC endpoint declared by the session generation. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="ProjectFingerprint" /> or <paramref name="Endpoint" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when one value violates the bootstrap argument contract. </exception>
    [JsonConstructor]
    public IpcDaemonBootstrapArguments (
        string RepositoryRoot,
        ProjectFingerprint ProjectFingerprint,
        string SessionPath,
        Guid SessionGenerationId,
        DateTimeOffset SessionIssuedAtUtc,
        IpcEndpoint Endpoint)
    {
        this.RepositoryRoot = ContractArgumentGuard.RequireValue(RepositoryRoot, nameof(RepositoryRoot));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.SessionPath = ContractArgumentGuard.RequireValue(SessionPath, nameof(SessionPath));
        this.SessionGenerationId = ContractArgumentGuard.RequireNonEmptyGuid(SessionGenerationId, nameof(SessionGenerationId));
        this.SessionIssuedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(SessionIssuedAtUtc, nameof(SessionIssuedAtUtc));
        this.Endpoint = ContractArgumentGuard.RequireNotNull(Endpoint, nameof(Endpoint));
    }

    /// <summary> Gets the non-empty repository root path. </summary>
    public string RepositoryRoot { get; }

    /// <summary> Gets the project fingerprint. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the non-empty daemon session file path. </summary>
    public string SessionPath { get; }

    /// <summary> Gets the non-empty daemon session generation identifier. </summary>
    public Guid SessionGenerationId { get; }

    /// <summary> Gets the daemon session issuance timestamp in UTC. </summary>
    public DateTimeOffset SessionIssuedAtUtc { get; }

    /// <summary> Gets the validated IPC endpoint. </summary>
    public IpcEndpoint Endpoint { get; }
}
