namespace MackySoft.Ucli.Features.Daemon.Supervisor;

/// <summary> Represents persisted runtime metadata for one worktree-local supervisor instance. </summary>
internal sealed record SupervisorInstanceManifest (
    int ProcessId,
    string SessionToken,
    string EndpointTransportKind,
    string EndpointAddress,
    DateTimeOffset IssuedAtUtc);