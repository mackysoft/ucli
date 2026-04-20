namespace MackySoft.Ucli.Features.Daemon.UseCases.Status;

/// <summary> Represents normalized payload values for one daemon-status command execution. </summary>
/// <param name="DaemonStatus"> The daemon-status value. </param>
/// <param name="ServerVersion"> The daemon server version when available; otherwise <see langword="null" />. </param>
/// <param name="Runtime"> The daemon runtime value when available; otherwise <see langword="null" />. </param>
/// <param name="LifecycleState"> The daemon lifecycle-state value when available; otherwise <see langword="null" />. </param>
/// <param name="BlockingReason"> The daemon blocking-reason value when available; otherwise <see langword="null" />. </param>
/// <param name="CompileState"> The daemon compile-state value when available; otherwise <see langword="null" />. </param>
/// <param name="CompileGeneration"> The daemon compile generation when available; otherwise <see langword="null" />. </param>
/// <param name="DomainReloadGeneration"> The daemon domain-reload generation when available; otherwise <see langword="null" />. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon status workflow. </param>
/// <param name="Session"> The daemon session values when available; otherwise <see langword="null" />. </param>
/// <param name="Diagnosis"> The daemon diagnosis values when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonStatusExecutionOutput (
    string DaemonStatus,
    string? ServerVersion,
    string? Runtime,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    int TimeoutMilliseconds,
    DaemonSessionOutput? Session,
    DaemonDiagnosisOutput? Diagnosis);