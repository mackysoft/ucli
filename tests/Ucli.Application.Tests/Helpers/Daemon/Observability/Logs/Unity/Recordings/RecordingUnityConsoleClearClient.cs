using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingUnityConsoleClearClient : IUnityConsoleClearClient
{
    private readonly UnityConsoleClearClientResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingUnityConsoleClearClient (UnityConsoleClearClientResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityConsoleClearClientResult> ClearAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProject,
            timeout,
            cancellationToken));

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
