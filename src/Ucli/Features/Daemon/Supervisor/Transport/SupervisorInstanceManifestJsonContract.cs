namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Represents raw JSON fields persisted for one supervisor instance. </summary>
internal sealed record SupervisorInstanceManifestJsonContract (
    int ProcessId,
    string? SessionToken,
    string? EndpointTransportKind,
    string? EndpointAddress,
    DateTimeOffset IssuedAtUtc)
{
    /// <inheritdoc />
    public override string ToString () => nameof(SupervisorInstanceManifestJsonContract);
}
