namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>ping</c> IPC response payload. </summary>
/// <param name="ServerVersion"> The server version string. </param>
/// <param name="EditorMode"> The daemon Editor mode identifier. </param>
/// <param name="UnityVersion"> The Unity editor version. </param>
/// <param name="ProjectFingerprint"> The Unity project fingerprint served by this IPC host. </param>
/// <param name="CompileState"> The compile-state value. </param>
/// <param name="LifecycleState"> The editor lifecycle-state value. </param>
/// <param name="BlockingReason"> The editor blocking-reason value. </param>
/// <param name="CompileGeneration"> The opaque compile generation. </param>
/// <param name="DomainReloadGeneration"> The opaque domain-reload generation. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
/// <param name="ObservedAtUtc"> The UTC timestamp when lifecycle values were observed. </param>
/// <param name="ActionRequired"> The normalized action required to resolve the current lifecycle state. </param>
/// <param name="PrimaryDiagnostic"> The primary machine-readable diagnostic for the current lifecycle state. </param>
/// <param name="PlayMode"> The Play Mode subsystem snapshot. </param>
public sealed record IpcPingResponse (
    string ServerVersion,
    string EditorMode,
    string UnityVersion,
    string ProjectFingerprint,
    string CompileState,
    string? LifecycleState = null,
    string? BlockingReason = null,
    string? CompileGeneration = null,
    string? DomainReloadGeneration = null,
    bool CanAcceptExecutionRequests = false,
    DateTimeOffset? ObservedAtUtc = null,
    string? ActionRequired = null,
    IpcPrimaryDiagnostic? PrimaryDiagnostic = null,
    IpcPlayModeSnapshot? PlayMode = null);
