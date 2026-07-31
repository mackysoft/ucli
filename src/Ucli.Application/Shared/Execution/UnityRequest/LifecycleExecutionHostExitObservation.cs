using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary>
/// Records that the operating-system process generation fixed by a durable Lifecycle Execution
/// start was confirmed to have exited.
/// </summary>
internal sealed record LifecycleExecutionHostExitObservation
{
    /// <summary> Initializes one confirmed fixed-host exit observation. </summary>
    public LifecycleExecutionHostExitObservation (ProcessIdentity process)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
    }

    /// <summary> Gets the exact process generation confirmed to have exited. </summary>
    public ProcessIdentity Process { get; }
}
