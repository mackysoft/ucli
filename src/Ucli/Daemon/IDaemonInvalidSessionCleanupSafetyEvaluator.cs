using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Evaluates whether invalid daemon session artifacts must be skipped as unsafe. </summary>
internal interface IDaemonInvalidSessionCleanupSafetyEvaluator
{
    /// <summary> Determines whether invalid daemon session artifacts must be skipped as unsafe. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The parsed invalid daemon session snapshot when available; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when invalid session must be skipped as unsafe; otherwise <see langword="false" />. </returns>
    bool RequiresUnsafeSkip (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? session);
}