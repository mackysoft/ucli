using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Owns one detached supervisor process until bootstrap commits or rolls back that generation. </summary>
internal sealed class DetachedSupervisorProcessLaunchLease : ISupervisorProcessLaunchLease
{
    private IDetachedProcessHandle? processHandle;

    /// <summary> Initializes a new instance of the <see cref="DetachedSupervisorProcessLaunchLease" /> class. </summary>
    /// <param name="processHandle"> The owned process generation. </param>
    public DetachedSupervisorProcessLaunchLease (IDetachedProcessHandle processHandle)
    {
        this.processHandle = processHandle ?? throw new ArgumentNullException(nameof(processHandle));
    }

    /// <inheritdoc />
    public async ValueTask CommitAsync ()
    {
        var handle = processHandle;
        if (handle is null)
        {
            return;
        }

        processHandle = null;
        try
        {
            await handle.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The ready supervisor owns its lifetime after commit; local handle-release failure must not terminate it.
        }
    }

    /// <inheritdoc />
    public async ValueTask<ExecutionError?> RollbackAsync ()
    {
        var handle = processHandle;
        if (handle is null)
        {
            return null;
        }

        try
        {
            var terminationResult = await handle.TerminateAsync(
                    ProcessTerminationPolicy.ForceKill,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (terminationResult == ProcessTerminationResult.ForceKillFailed)
            {
                return ExecutionError.InternalError(
                    "Detached supervisor process termination could not be confirmed.");
            }

            processHandle = null;
            try
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The process generation is already stopped; local handle-release failure does not invalidate rollback.
            }

            return null;
        }
        catch (Exception exception)
        {
            return ExecutionError.InternalError(
                $"Failed to roll back detached supervisor process. {exception.Message}");
        }
    }
}
