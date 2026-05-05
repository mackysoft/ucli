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
using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Applies cleanup compensation when daemon launch workflow fails. </summary>
internal interface IDaemonLaunchCompensationService
{
    /// <summary> Stops the launched process snapshot and cleans daemon artifacts after launch failure. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon-session issuance timestamp used for identity validation. </param>
    /// <param name="timeout"> The remaining timeout budget for launch-failure compensation. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The compensation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunch (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset? expectedIssuedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
