using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Owns a Unity process handle together with its persisted oneshot bootstrap generation. </summary>
internal sealed class OneshotBootstrapOwnedProcessHandle : IUnityBatchmodeProcessHandle
{
    private readonly IUnityBatchmodeProcessHandle innerHandle;

    private readonly string storageRoot;

    private readonly IpcOneshotBootstrapEnvelope bootstrapEnvelope;

    private int disposed;

    /// <summary> Initializes ownership of one process handle and bootstrap generation. </summary>
    public OneshotBootstrapOwnedProcessHandle (
        IUnityBatchmodeProcessHandle innerHandle,
        string storageRoot,
        IpcOneshotBootstrapEnvelope bootstrapEnvelope)
    {
        this.innerHandle = innerHandle ?? throw new ArgumentNullException(nameof(innerHandle));
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        this.storageRoot = storageRoot;
        this.bootstrapEnvelope = bootstrapEnvelope ?? throw new ArgumentNullException(nameof(bootstrapEnvelope));
    }

    /// <inheritdoc />
    public int ProcessId => innerHandle.ProcessId;

    /// <inheritdoc />
    public DateTimeOffset? StartTimeUtc => innerHandle.StartTimeUtc;

    /// <inheritdoc />
    public bool HasExited => innerHandle.HasExited;

    /// <inheritdoc />
    public int? ExitCode => innerHandle.ExitCode;

    /// <inheritdoc />
    public Task<ProcessTerminationResult> TerminateAsync (
        ProcessTerminationPolicy terminationPolicy,
        CancellationToken cancellationToken)
    {
        return innerHandle.TerminateAsync(terminationPolicy, cancellationToken);
    }

    /// <inheritdoc />
    public Task WaitForExitAsync (CancellationToken cancellationToken = default)
    {
        return innerHandle.WaitForExitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync ()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await innerHandle.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(storageRoot, bootstrapEnvelope);
        }
    }
}
