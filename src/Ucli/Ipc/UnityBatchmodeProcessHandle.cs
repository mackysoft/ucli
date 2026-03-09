using System.Diagnostics;

namespace MackySoft.Ucli.Ipc;

/// <summary> Wraps one started Unity batchmode process handle. </summary>
internal sealed class UnityBatchmodeProcessHandle : IUnityBatchmodeProcessHandle
{
    private readonly Process process;

    private bool disposed;

    /// <summary> Initializes a new instance of the <see cref="UnityBatchmodeProcessHandle" /> class. </summary>
    /// <param name="process"> The started process instance. </param>
    public UnityBatchmodeProcessHandle (Process process)
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
    public Task WaitForExit (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return process.WaitForExitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task Terminate (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
                // NOTE: The child process may already be terminating by the time cleanup runs.
            }
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // NOTE: The child process may already be fully terminated before wait begins.
        }
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
