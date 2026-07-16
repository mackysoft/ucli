using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingSupervisorProcessLaunchLease : ISupervisorProcessLaunchLease
{
    public Func<ValueTask>? CommitHandler { get; set; }

    public Func<ValueTask<ExecutionError?>>? RollbackHandler { get; set; }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public async ValueTask CommitAsync ()
    {
        CommitCount++;
        if (CommitHandler is not null)
        {
            await CommitHandler().ConfigureAwait(false);
        }
    }

    public async ValueTask<ExecutionError?> RollbackAsync ()
    {
        RollbackCount++;
        return RollbackHandler is null
            ? null
            : await RollbackHandler().ConfigureAwait(false);
    }
}
