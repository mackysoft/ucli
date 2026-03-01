using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Launches Unity batchmode processes configured to run as uCLI daemon servers. </summary>
internal interface IUnityDaemonProcessLauncher
{
    /// <summary> Launches one Unity batchmode daemon process for the specified project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="daemonLogPath"> The daemon log file path passed to Unity <c>-logFile</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon launch result. </returns>
    ValueTask<UnityDaemonLaunchResult> Launch (
        ResolvedUnityProjectContext unityProject,
        string daemonLogPath,
        CancellationToken cancellationToken = default);
}