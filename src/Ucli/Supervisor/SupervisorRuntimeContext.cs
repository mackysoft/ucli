namespace MackySoft.Ucli.Supervisor;

/// <summary> Represents immutable runtime metadata owned by one supervisor host instance. </summary>
internal sealed record SupervisorRuntimeContext (
    string StorageRoot,
    SupervisorInstanceManifest Manifest);