using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Evaluates whether invalid daemon session artifacts must be skipped as unsafe. </summary>
internal interface IDaemonInvalidSessionCleanupSafetyEvaluator
{
    /// <summary> Determines whether invalid daemon session artifacts must be skipped as unsafe. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="evidence"> The restricted invalid daemon session evidence when available; otherwise <see langword="null" />. </param>
    /// <returns>
    /// <para> <see langword="true" /> when the parsed invalid session still provides enough live-process evidence that cleanup must be skipped immediately as unsafe. </para>
    /// <para> <see langword="false" /> when this evaluator does not force an unsafe skip; callers still need separate endpoint evidence before destructive cleanup. </para>
    /// </returns>
    bool RequiresUnsafeSkip (
        ResolvedUnityProjectContext unityProject,
        DaemonInvalidSessionEvidence? evidence);
}
