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
using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Cleans safe daemon artifacts for one Unity project context. </summary>
internal interface IDaemonCleanupOperation
{
    /// <summary> Cleans safe daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon cleanup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One daemon cleanup result. </para>
    /// <para> <see cref="DaemonCleanupStatus.Completed" /> means the operation completed one safe cleanup path; it does not guarantee that artifacts were present and deleted. </para>
    /// <para> <see cref="DaemonCleanupStatus.Skipped" /> is a successful non-destructive outcome used when cleanup cannot safely prove that deleting canonical artifacts is allowed. </para>
    /// <para> <see cref="DaemonCleanupStatus.Failed" /> is reserved for timeout, I/O, and unexpected internal failures. </para>
    /// </returns>
    ValueTask<DaemonCleanupResult> Cleanup (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
