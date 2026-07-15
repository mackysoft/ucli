using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents the persisted GUI supervisor endpoint and identity contract. </summary>
internal sealed record GuiSupervisorManifestJsonContract
{
    /// <summary> Current GUI supervisor manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Initializes one validated GUI supervisor manifest contract. </summary>
    public GuiSupervisorManifestJsonContract (
        int SchemaVersion,
        IpcSessionToken SessionToken,
        ProjectFingerprint ProjectFingerprint,
        IpcEndpoint Endpoint,
        int ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DateTimeOffset IssuedAtUtc)
    {
        this.SchemaVersion = ContractArgumentGuard.RequirePositive(SchemaVersion, nameof(SchemaVersion));
        this.SessionToken = ContractArgumentGuard.RequireNotNull(SessionToken, nameof(SessionToken));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.Endpoint = ContractArgumentGuard.RequireNotNull(Endpoint, nameof(Endpoint));
        this.ProcessId = ContractArgumentGuard.RequirePositive(ProcessId, nameof(ProcessId));
        this.ProcessStartedAtUtc = ProcessStartedAtUtc is DateTimeOffset processStartedAtUtc
            ? ContractArgumentGuard.RequireUtcTimestamp(processStartedAtUtc, nameof(ProcessStartedAtUtc))
            : null;
        this.IssuedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(IssuedAtUtc, nameof(IssuedAtUtc));
    }

    public int SchemaVersion { get; }

    public IpcSessionToken SessionToken { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public IpcEndpoint Endpoint { get; }

    public int ProcessId { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public DateTimeOffset IssuedAtUtc { get; }

    /// <inheritdoc />
    public override string ToString () => nameof(GuiSupervisorManifestJsonContract);
}
