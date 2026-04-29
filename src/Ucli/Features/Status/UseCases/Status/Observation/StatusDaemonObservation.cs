namespace MackySoft.Ucli.Features.Status.UseCases.Status.Observation;

/// <summary> Represents daemon observation values that can be projected into status command payload. </summary>
/// <param name="DaemonStatus"> The daemon status value serialized into command payload. </param>
/// <param name="ServerVersion"> The daemon-side server version when reachable. </param>
/// <param name="LifecycleState"> The daemon-side lifecycle-state when reachable. </param>
/// <param name="BlockingReason"> The daemon-side blocking-reason when reachable. </param>
/// <param name="CompileState"> The daemon compile-state value when reachable. </param>
/// <param name="CompileGeneration"> The daemon compile generation when reachable. </param>
/// <param name="DomainReloadGeneration"> The daemon domain-reload generation when reachable. </param>
/// <param name="CanAcceptExecutionRequests"> Whether the daemon can currently accept execution requests. </param>
/// <param name="Runtime"> The daemon runtime value when reachable. </param>
internal sealed record StatusDaemonObservation (
    string DaemonStatus,
    string? ServerVersion,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    string? Runtime);
