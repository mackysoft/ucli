namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Represents immutable runtime metadata owned by one supervisor host instance. </summary>
internal sealed record SupervisorRuntimeContext (
    string StorageRoot,
    SupervisorInstanceManifest Manifest);
