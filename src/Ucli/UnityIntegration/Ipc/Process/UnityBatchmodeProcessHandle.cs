namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Wraps one started Unity batchmode process handle. </summary>
internal sealed class UnityBatchmodeProcessHandle : IUnityBatchmodeProcessHandle
{
    private readonly System.Diagnostics.Process process;

    private bool disposed;

    /// <summary> Initializes a new instance of the <see cref="UnityBatchmodeProcessHandle" /> class. </summary>
    /// <param name="process"> The started process instance. </param>
    public UnityBatchmodeProcessHandle (System.Diagnostics.Process process)
    {
        this.process = process ?? throw new ArgumentNullException(nameof(process));
    }

    /// <inheritdoc />
    public int ProcessId => process.Id;

    /// <inheritdoc />
    public bool HasExited => process.HasExited;

    /// <inheritdoc />
    public int? ExitCode => process.HasExited ? process.ExitCode : null;

    /// <inheritdoc />
    public Task WaitForExitAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return process.WaitForExitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProcessTerminationResult> TerminateAsync (
        ProcessTerminationPolicy? terminationPolicy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ProcessTerminator.TerminateAsync(process, terminationPolicy, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync ()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        process.Dispose();
        return ValueTask.CompletedTask;
    }
}
