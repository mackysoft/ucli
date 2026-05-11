using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Represents the persisted GUI supervisor endpoint and identity metadata. </summary>
internal sealed record GuiSupervisorManifest (
    int SchemaVersion,
    string SessionToken,
    string ProjectFingerprint,
    string EndpointTransportKind,
    string EndpointAddress,
    int ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    DateTimeOffset IssuedAtUtc)
{
    public const int CurrentSchemaVersion = 1;

    public IpcEndpoint ResolveEndpoint ()
    {
        if (!IpcTransportKindCodec.TryParse(EndpointTransportKind, out var transportKind))
        {
            throw new InvalidDataException($"GUI supervisor endpointTransportKind is invalid: {EndpointTransportKind}.");
        }

        return new IpcEndpoint(transportKind, EndpointAddress);
    }
}
