using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class UnexpectedDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
{
    private readonly string reason;

    public UnexpectedDaemonSessionConnectionProvider (string reason)
    {
        this.reason = string.IsNullOrWhiteSpace(reason)
            ? "Daemon session connection should not be resolved."
            : reason;
    }

    public ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
