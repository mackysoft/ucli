using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Infrastructure.Execution;

namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary>Observes exact process generations through the host operating system.</summary>
internal sealed class ProcessIdentityObserver : IProcessIdentityObserver
{
    /// <inheritdoc />
    public ProcessIdentityStatus Observe (ProcessIdentity process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return ProcessLivenessProbe.ObserveIdentity(process) switch
        {
            ProcessIdentityObservation.Same => ProcessIdentityStatus.Matching,
            ProcessIdentityObservation.ConfirmedExitedOrReplaced =>
                ProcessIdentityStatus.ExitedOrReplaced,
            ProcessIdentityObservation.Unobservable => ProcessIdentityStatus.Unobservable,
            _ => throw new InvalidOperationException("Process identity observation is undefined."),
        };
    }
}
