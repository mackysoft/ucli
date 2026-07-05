using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedUnityConsoleClearClient : IUnityConsoleClearClient
{
    private readonly string reason;

    public UnexpectedUnityConsoleClearClient (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<UnityConsoleClearClientResult> ClearAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
