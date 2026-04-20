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
namespace MackySoft.Ucli.Features.Daemon.UseCases.Inventory;

/// <summary> Represents one daemon registration observation returned by <c>daemon list</c>. </summary>
/// <param name="WorktreePath"> The Git worktree root path. </param>
/// <param name="BranchRef"> The branch ref when attached; otherwise <see langword="null" />. </param>
/// <param name="Head"> The HEAD commit hash. </param>
/// <param name="ProjectPath"> The Unity project root path under the worktree. </param>
/// <param name="ProjectFingerprint"> The deterministic project fingerprint resolved for the worktree-local storage root. </param>
/// <param name="State"> The daemon observation state. </param>
/// <param name="Reason"> The optional failure reason for non-running states. </param>
/// <param name="IssuedAtUtc"> The daemon session issuance timestamp when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="ProcessId"> The daemon process identifier when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="EndpointTransportKind"> The daemon endpoint transport kind when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="EndpointAddress"> The daemon endpoint address when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="Diagnosis"> The daemon diagnosis values when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonListItemOutput (
    string WorktreePath,
    string? BranchRef,
    string Head,
    string ProjectPath,
    string ProjectFingerprint,
    string State,
    string? Reason,
    DateTimeOffset? IssuedAtUtc,
    int? ProcessId,
    string? EndpointTransportKind,
    string? EndpointAddress,
    DaemonDiagnosisOutput? Diagnosis);