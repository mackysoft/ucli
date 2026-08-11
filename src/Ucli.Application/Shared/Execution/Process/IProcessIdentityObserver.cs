using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Shared.Execution.Process;

/// <summary>Observes whether one exact operating-system process generation still exists.</summary>
internal interface IProcessIdentityObserver
{
    /// <summary>Observes the current state of the specified process generation.</summary>
    ProcessIdentityStatus Observe (ProcessIdentity process);
}
