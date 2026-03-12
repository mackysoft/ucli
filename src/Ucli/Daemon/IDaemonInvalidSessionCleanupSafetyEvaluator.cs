using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Evaluates whether invalid daemon session artifacts can be cleaned safely. </summary>
internal interface IDaemonInvalidSessionCleanupSafetyEvaluator
{
    /// <summary> Determines whether invalid daemon session artifacts can be cleaned safely. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The parsed invalid daemon session snapshot when available; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when cleanup is safe; otherwise <see langword="false" />. </returns>
    bool CanCleanup (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? session);
}