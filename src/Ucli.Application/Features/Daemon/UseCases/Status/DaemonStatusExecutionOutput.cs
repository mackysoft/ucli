using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

/// <summary> Represents normalized payload values for one daemon-status command execution. </summary>
/// <param name="DaemonStatus"> The daemon-status value. </param>
/// <param name="ServerVersion"> The daemon server version when available; otherwise <see langword="null" />. </param>
/// <param name="EditorMode"> The daemon Editor mode when available; otherwise <see langword="null" />. </param>
/// <param name="LifecycleState"> The daemon lifecycle-state value when available; otherwise <see langword="null" />. </param>
/// <param name="BlockingReason"> The daemon blocking-reason value when available; otherwise <see langword="null" />. </param>
/// <param name="CompileState"> The daemon compile-state value when available; otherwise <see langword="null" />. </param>
/// <param name="CompileGeneration"> The daemon compile generation when available; otherwise <see langword="null" />. </param>
/// <param name="DomainReloadGeneration"> The daemon domain-reload generation when available; otherwise <see langword="null" />. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
/// <param name="ObservedAtUtc"> The daemon lifecycle observation timestamp when available. </param>
/// <param name="ActionRequired"> The normalized user action required by the lifecycle blocker when available. </param>
/// <param name="PrimaryDiagnostic"> The primary lifecycle diagnostic when available. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon status workflow. </param>
/// <param name="Session"> The daemon session values when available; otherwise <see langword="null" />. </param>
/// <param name="Diagnosis"> The daemon diagnosis values when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonStatusExecutionOutput (
    DaemonStatusKind DaemonStatus,
    string? ServerVersion,
    string? EditorMode,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    int TimeoutMilliseconds,
    DaemonSessionOutput? Session,
    DaemonDiagnosisOutput? Diagnosis,
    DateTimeOffset? ObservedAtUtc = null,
    string? ActionRequired = null,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic = null);
