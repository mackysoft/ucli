using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Owns one supervisor process generation until bootstrap either observes readiness or rolls it back. </summary>
internal interface ISupervisorProcessLaunchLease
{
    /// <summary>
    /// Transfers the launched generation to the ready supervisor and releases bootstrap-local resources without terminating it.
    /// After commit completes, rollback has no effect.
    /// </summary>
    ValueTask CommitAsync ();

    /// <summary>
    /// Stops only the launched generation represented by this lease and waits for termination.
    /// A failed rollback retains ownership so the same generation can be retried.
    /// </summary>
    /// <returns> One structured error when termination cannot be confirmed; otherwise <see langword="null" />. Successful rollback is idempotent. </returns>
    ValueTask<ExecutionError?> RollbackAsync ();
}
