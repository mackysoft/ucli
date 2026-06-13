namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the Unity editor lifecycle state observed around a build pipeline invocation. </summary>
/// <param name="ServerVersion"> The daemon server version string. </param>
/// <param name="EditorMode"> The daemon Editor mode identifier. </param>
/// <param name="UnityVersion"> The Unity editor version. </param>
/// <param name="ProjectFingerprint"> The Unity project fingerprint served by this IPC host. </param>
/// <param name="LifecycleState"> The editor lifecycle-state value. </param>
/// <param name="BlockingReason"> The editor blocking-reason value. </param>
/// <param name="CompileState"> The compile-state value. </param>
/// <param name="CompileGeneration"> The opaque compile generation. </param>
/// <param name="DomainReloadGeneration"> The opaque domain-reload generation. </param>
/// <param name="CanAcceptExecutionRequests"> Whether normal execution requests can currently be accepted. </param>
/// <param name="ObservedAtUtc"> The UTC timestamp when lifecycle values were observed. </param>
/// <param name="ActionRequired"> The normalized action required to resolve the current lifecycle state. </param>
/// <param name="PrimaryDiagnostic"> The primary machine-readable diagnostic for the current lifecycle state. </param>
/// <param name="PlayMode"> The Play Mode subsystem snapshot. </param>
/// <param name="AssetRefreshGeneration"> The opaque asset-refresh generation. </param>
public sealed record IpcBuildLifecycleSnapshot (
    string? ServerVersion,
    string? EditorMode,
    string? UnityVersion,
    string? ProjectFingerprint,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    IpcPrimaryDiagnostic? PrimaryDiagnostic,
    IpcPlayModeSnapshot? PlayMode,
    string? AssetRefreshGeneration = null);
