using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

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
    ValueTask<DaemonDiagnosis?> ResolveForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis,
        CancellationToken cancellationToken = default);
}
