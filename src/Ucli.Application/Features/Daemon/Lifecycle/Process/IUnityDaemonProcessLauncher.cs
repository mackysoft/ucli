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

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Launches Unity batchmode processes configured to run as uCLI daemon servers. </summary>
internal interface IUnityDaemonProcessLauncher
{
    /// <summary> Launches one Unity batchmode daemon process for the specified project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The persisted daemon session metadata associated with this launch. </param>
    /// <param name="unityLogPath"> The Unity log file path passed to Unity <c>-logFile</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon launch result. </returns>
    ValueTask<UnityDaemonLaunchResult> Launch (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        string unityLogPath,
        CancellationToken cancellationToken = default);
}
