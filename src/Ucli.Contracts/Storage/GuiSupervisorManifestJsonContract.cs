using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents the persisted GUI supervisor endpoint and identity contract. </summary>
internal sealed record GuiSupervisorManifestJsonContract
{
    /// <summary> Current GUI supervisor manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Initializes one validated GUI supervisor manifest contract. </summary>
    [JsonConstructor]
    public GuiSupervisorManifestJsonContract (
        int SchemaVersion,
        string SessionToken,
        ProjectFingerprint ProjectFingerprint,
        string EndpointTransportKind,
        string EndpointAddress,
        int ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DateTimeOffset IssuedAtUtc)
    {
        this.SchemaVersion = ContractArgumentGuard.RequirePositive(SchemaVersion, nameof(SchemaVersion));
        this.SessionToken = ContractArgumentGuard.RequireValue(SessionToken, nameof(SessionToken));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.EndpointTransportKind = ContractArgumentGuard.RequireValue(EndpointTransportKind, nameof(EndpointTransportKind));
        this.EndpointAddress = ContractArgumentGuard.RequireValue(EndpointAddress, nameof(EndpointAddress));
        this.ProcessId = ContractArgumentGuard.RequirePositive(ProcessId, nameof(ProcessId));
        this.ProcessStartedAtUtc = ProcessStartedAtUtc;
        this.IssuedAtUtc = IssuedAtUtc;
    }

    public int SchemaVersion { get; init; }

    public string SessionToken { get; init; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public string EndpointTransportKind { get; init; }

    public string EndpointAddress { get; init; }

    public int ProcessId { get; init; }

    public DateTimeOffset? ProcessStartedAtUtc { get; init; }

    public DateTimeOffset IssuedAtUtc { get; init; }

    /// <inheritdoc />
    public override string ToString () => nameof(GuiSupervisorManifestJsonContract);
}
