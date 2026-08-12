using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary>
/// Owns the process, bootstrap credentials, endpoint identity, and project lock fixed before a
/// Lifecycle Execution is admitted to its oneshot provider.
/// </summary>
internal sealed class OneshotHostLease : IAsyncDisposable
{
    private IAsyncDisposable? lifecycleLock;

    private IUnityBatchmodeProcessHandle? processHandle;

    private LifecycleExecutionHostRegistration? lifecycleHost;

    public OneshotHostLease (
        ResolvedUnityProjectContext project,
        IAsyncDisposable lifecycleLock,
        IUnityBatchmodeProcessHandle processHandle,
        IpcOneshotBootstrapEnvelope bootstrap,
        IpcTransportEndpoint endpoint)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        this.lifecycleLock = lifecycleLock ?? throw new ArgumentNullException(nameof(lifecycleLock));
        this.processHandle = processHandle ?? throw new ArgumentNullException(nameof(processHandle));
        Bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public ResolvedUnityProjectContext Project { get; }

    public IpcOneshotBootstrapEnvelope Bootstrap { get; }

    public IpcSessionToken SessionToken => Bootstrap.SessionToken;

    public IpcTransportEndpoint Endpoint { get; }

    public IUnityBatchmodeProcessHandle ProcessHandle => processHandle
        ?? throw new InvalidOperationException("The oneshot host lease no longer owns its process handle.");

    /// <summary> Accepts the one host registration proven by this launched process. </summary>
    public bool TryAcceptLifecycleHost (LifecycleExecutionHostRegistration host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.Process.ProcessId != ProcessHandle.ProcessId)
        {
            return false;
        }

        if (lifecycleHost is null)
        {
            lifecycleHost = host;
            return true;
        }

        return lifecycleHost == host;
    }

    /// <summary> Transfers the process handle to lifecycle recovery after a non-terminal durable start. </summary>
    public void RelinquishProcessOwnership ()
    {
        processHandle = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync ()
    {
        var ownedProcessHandle = processHandle;
        processHandle = null;
        var ownedLock = lifecycleLock;
        lifecycleLock = null;

        if (ownedProcessHandle is not null)
        {
            try
            {
                await ownedProcessHandle.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The dispatch outcome remains authoritative over handle-release failures.
            }
        }

        if (ownedLock is not null)
        {
            try
            {
                await ownedLock.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The dispatch outcome remains authoritative over lock-release failures.
            }
        }
    }
}
