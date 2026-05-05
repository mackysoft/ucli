using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Launches Unity batchmode child processes for IPC daemon and oneshot hosts. </summary>
internal interface IUnityBatchmodeProcessLauncher
{
    /// <summary> Launches one Unity batchmode child process with the specified bootstrap payload. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="bootstrapArguments"> The batchmode bootstrap payload written into command-line arguments. </param>
    /// <param name="unityLogPath"> The Unity log file path passed to Unity <c>-logFile</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch result that contains either the started process handle or one structured launch error. </returns>
    ValueTask<UnityBatchmodeProcessLaunchResult> Launch (
        ResolvedUnityProjectContext unityProject,
        IpcBatchmodeBootstrapArguments bootstrapArguments,
        string unityLogPath,
        CancellationToken cancellationToken = default);
}
