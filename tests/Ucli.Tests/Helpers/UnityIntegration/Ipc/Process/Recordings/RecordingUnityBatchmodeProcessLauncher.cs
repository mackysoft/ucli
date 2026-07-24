using MackySoft.FileSystem;
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

    public ValueTask<UnityBatchmodeProcessLaunchResult> LaunchOneshotAsync (
        ResolvedUnityProjectContext unityProject,
        IpcOneshotBootstrapEnvelope bootstrapEnvelope,
        AbsolutePath unityLogPath,
        UnityBatchmodeLaunchOptions launchOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProject,
            bootstrapEnvelope,
            unityLogPath,
            launchOptions,
            cancellationToken));
        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        IpcOneshotBootstrapEnvelope BootstrapEnvelope,
        AbsolutePath UnityLogPath,
        UnityBatchmodeLaunchOptions LaunchOptions,
        CancellationToken CancellationToken);
}
