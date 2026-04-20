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

/// <summary> Represents normalized payload values for one daemon-list command execution. </summary>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon probing. </param>
/// <param name="ProjectRelativePath"> The current Unity project path relative to the current Git worktree root. </param>
/// <param name="IsComplete"> Whether all candidate worktrees were fully observed before the shared deadline expired. </param>
/// <param name="CompletionReason"> The reason when the result is partial; otherwise <see langword="null" />. </param>
/// <param name="RemainingWorktreeCount"> The number of worktrees left unobserved when the result is partial; otherwise <c>0</c>. </param>
/// <param name="Items"> The daemon registration observations returned from worktree enumeration. </param>
internal sealed record DaemonListExecutionOutput (
    int TimeoutMilliseconds,
    string ProjectRelativePath,
    bool IsComplete,
    string? CompletionReason,
    int RemainingWorktreeCount,
    IReadOnlyList<DaemonListItemOutput> Items);