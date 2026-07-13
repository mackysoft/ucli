using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedDaemonPingInfoClient : IDaemonPingInfoClient
{
    private readonly string reason;

    public UnexpectedDaemonPingInfoClient (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<IpcUnityEditorObservation> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        bool validateProjectFingerprint = true,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
