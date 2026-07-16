using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Owns the operating-system handle for one started detached process generation. </summary>
internal sealed class DetachedProcessHandle : IDetachedProcessHandle
{
    private readonly DiagnosticsProcess process;

    private bool disposed;

    /// <summary> Initializes a new instance of the <see cref="DetachedProcessHandle" /> class. </summary>
    /// <param name="process"> The started process whose handle is transferred to this instance. </param>
    public DetachedProcessHandle (DiagnosticsProcess process)
    {
        this.process = process ?? throw new ArgumentNullException(nameof(process));
    }

    /// <inheritdoc />
    public Task<ProcessTerminationResult> TerminateAsync (
        ProcessTerminationPolicy terminationPolicy,
        CancellationToken cancellationToken)
    {
        return ProcessTerminator.TerminateAsync(process, terminationPolicy, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync ()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        process.Dispose();
        disposed = true;
        return ValueTask.CompletedTask;
    }
}
