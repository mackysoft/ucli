using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

namespace MackySoft.Ucli.Application.Features.Status.Common.Contracts;

/// <summary> Represents normalized output payload values for one status command execution. </summary>
/// <param name="DaemonStatus"> The daemon status value. </param>
/// <param name="UnityVersion"> The Unity editor version resolved from <c>ProjectVersion.txt</c>. </param>
/// <param name="ServerVersion"> The daemon-side server version when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="LifecycleState"> The daemon-side lifecycle-state when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="BlockingReason"> The daemon-side blocking-reason when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="CompileState"> The daemon compile-state value when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="CompileGeneration"> The daemon compile generation when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="DomainReloadGeneration"> The daemon domain-reload generation when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
/// <param name="EditorMode"> The daemon Editor mode when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon status probing. </param>
internal sealed record StatusExecutionOutput (
    DaemonStatusKind DaemonStatus,
    string UnityVersion,
    string? ServerVersion,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    string? EditorMode,
    int TimeoutMilliseconds);
