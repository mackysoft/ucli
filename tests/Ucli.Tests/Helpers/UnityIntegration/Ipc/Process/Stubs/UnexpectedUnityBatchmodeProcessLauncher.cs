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

    public ValueTask<UnityBatchmodeProcessLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        IpcBatchmodeBootstrapArguments bootstrapArguments,
        string unityLogPath,
        UnityBatchmodeLaunchOptions? launchOptions = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
