using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedLifecycleExecutionHostExitTerminalizer :
    ILifecycleExecutionHostExitTerminalizer
{
    public ValueTask<LifecycleExecutionHostExitTerminalizationResult>
        TerminalizeAsync (
            ResolvedUnityProjectContext project,
            LifecycleExecutionStartBinding start,
            ExecutionRef currentReference,
            LifecycleExecutionTerminalFacts observedTerminalFacts,
            LifecycleExecutionHostExitTerminalRecordFactory
                terminalRecordFactory)
    {
        throw new InvalidOperationException(
            "The workflow must not terminalize a confirmed fixed-host exit.");
    }
}
