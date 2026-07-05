using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Helpers.Process;

internal sealed class RecordingUnityBatchmodeProcessLauncher : IUnityBatchmodeProcessLauncher
{
    private readonly UnityBatchmodeProcessLaunchResult result;

    private readonly List<Invocation> invocations = [];

    public RecordingUnityBatchmodeProcessLauncher (UnityBatchmodeProcessLaunchResult result)
    {
        this.result = result;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityBatchmodeProcessLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        IpcBatchmodeBootstrapArguments bootstrapArguments,
        string unityLogPath,
        UnityBatchmodeLaunchOptions? launchOptions = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProject,
            bootstrapArguments,
            unityLogPath,
            launchOptions,
            cancellationToken));
        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        IpcBatchmodeBootstrapArguments BootstrapArguments,
        string UnityLogPath,
        UnityBatchmodeLaunchOptions? LaunchOptions,
        CancellationToken CancellationToken);
}
