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

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Resolves daemon diagnosis metadata for one observed daemon session. </summary>
internal interface IDaemonSessionDiagnosisResolver
{
    /// <summary> Resolves persisted or synthesized diagnosis for one daemon session. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The daemon session metadata. </param>
    /// <param name="persistedDiagnosis"> The previously persisted diagnosis metadata when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The matched or synthesized daemon diagnosis metadata when available. </returns>
    ValueTask<DaemonDiagnosis?> ResolveForSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis,
        CancellationToken cancellationToken = default);
}
