using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Launches Unity batchmode child processes for IPC oneshot hosts. </summary>
internal interface IUnityBatchmodeProcessLauncher
{
    /// <summary> Persists and launches one Unity batchmode child process with the specified oneshot bootstrap generation. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="bootstrapEnvelope"> The immutable secret-bearing oneshot bootstrap generation. </param>
    /// <param name="unityLogPath"> The Unity log file path passed to Unity <c>-logFile</c>. </param>
    /// <param name="launchOptions"> The Unity process launch options outside the uCLI bootstrap payload. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch result that contains either the started process handle or one structured launch error. </returns>
    ValueTask<UnityBatchmodeProcessLaunchResult> LaunchOneshotAsync (
        ResolvedUnityProjectContext unityProject,
        IpcOneshotBootstrapEnvelope bootstrapEnvelope,
        string unityLogPath,
        UnityBatchmodeLaunchOptions launchOptions,
        CancellationToken cancellationToken);
}
