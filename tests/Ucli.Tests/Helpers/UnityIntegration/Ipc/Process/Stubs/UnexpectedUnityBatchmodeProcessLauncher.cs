using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Helpers.Process;

internal sealed class UnexpectedUnityBatchmodeProcessLauncher : IUnityBatchmodeProcessLauncher
{
    private readonly string reason;

    public UnexpectedUnityBatchmodeProcessLauncher (string reason)
    {
        this.reason = string.IsNullOrWhiteSpace(reason)
            ? "Unity batchmode process should not be launched."
            : reason;
    }

    public ValueTask<UnityBatchmodeProcessLaunchResult> LaunchOneshotAsync (
        ResolvedUnityProjectContext unityProject,
        IpcOneshotBootstrapEnvelope bootstrapEnvelope,
        AbsolutePath unityLogPath,
        UnityBatchmodeLaunchOptions launchOptions,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }
}
