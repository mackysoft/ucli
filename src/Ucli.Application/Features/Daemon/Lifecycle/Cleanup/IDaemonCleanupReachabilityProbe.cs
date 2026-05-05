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
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Probes daemon reachability using cleanup-specific safety semantics. </summary>
internal interface IDaemonCleanupReachabilityProbe
{
    /// <summary> Probes whether cleanup has endpoint-level evidence that the canonical daemon endpoint is absent. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="sessionToken"> The probe session token to send. Must be non-empty. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One cleanup-specific reachability probe result. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.NotRunning" /> means the probe observed direct endpoint-level absence evidence that is strong enough for destructive cleanup. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Running" /> means the daemon accepted the ping request successfully. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Uncertain" /> means cleanup could not safely prove endpoint absence and must not delete artifacts based on probe evidence alone. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Failed" /> means probing itself failed unexpectedly. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="sessionToken" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    ValueTask<DaemonCleanupReachabilityProbeResult> Probe (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        string sessionToken,
        CancellationToken cancellationToken = default);
}
