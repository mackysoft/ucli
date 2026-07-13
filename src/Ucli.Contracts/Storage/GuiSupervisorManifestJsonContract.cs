namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents the persisted GUI supervisor endpoint and identity contract. </summary>
internal sealed record GuiSupervisorManifestJsonContract (
    int SchemaVersion,
    string SessionToken,
    string ProjectFingerprint,
    string EndpointTransportKind,
    string EndpointAddress,
    int ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    DateTimeOffset IssuedAtUtc)
{
    /// <summary> Current GUI supervisor manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <inheritdoc />
    public override string ToString () => nameof(GuiSupervisorManifestJsonContract);
}
