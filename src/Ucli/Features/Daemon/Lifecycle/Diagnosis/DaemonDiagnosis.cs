using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Represents one structured daemon termination diagnosis associated with one daemon lifecycle. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason value. </param>
/// <param name="Message"> The human-readable daemon diagnosis message. </param>
/// <param name="ReportedBy"> The normalized subject that reported this daemon diagnosis. </param>
/// <param name="IsInferred"> <see langword="true" /> when CLI inferred the diagnosis after observation; otherwise <see langword="false" />. </param>
/// <param name="UpdatedAtUtc"> The UTC timestamp when the diagnosis was recorded or derived. </param>
/// <param name="ProcessId"> The daemon process identifier when available. </param>
/// <param name="SessionIssuedAtUtc"> The daemon session issuance timestamp associated with the diagnosis. </param>
internal sealed record DaemonDiagnosis (
    string Reason,
    string Message,
    string ReportedBy,
    bool IsInferred,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId,
    DateTimeOffset SessionIssuedAtUtc);
