namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>ping</c> IPC response payload. </summary>
/// <param name="ServerVersion"> The server version string. </param>
/// <param name="Runtime"> The server runtime identifier. </param>
/// <param name="UnityVersion"> The Unity editor version. </param>
/// <param name="CompileState"> The compile-state value. </param>
/// <param name="LifecycleState"> The editor lifecycle-state value. </param>
/// <param name="BlockingReason"> The editor blocking-reason value. </param>
/// <param name="CompileGeneration"> The opaque compile generation. </param>
/// <param name="DomainReloadGeneration"> The opaque domain-reload generation. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
public sealed record IpcPingResponse (
    string ServerVersion,
    string Runtime,
    string UnityVersion,
    string CompileState,
    string? LifecycleState = null,
    string? BlockingReason = null,
    string? CompileGeneration = null,
    string? DomainReloadGeneration = null,
    bool CanAcceptExecutionRequests = false);
