using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

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
    DaemonStatusKind DaemonStatus,
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
