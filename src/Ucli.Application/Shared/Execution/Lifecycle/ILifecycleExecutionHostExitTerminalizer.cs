using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Reconciles durable execution state, then publishes and reverifies an action-owned Terminal
/// Record after its fixed Unity host exits.
/// </summary>
internal interface ILifecycleExecutionHostExitTerminalizer
{
    /// <summary>
    /// Conservatively merges the caller observation with durable side-effect admission before
    /// asking the action factory for the concrete Terminal Record.
    /// </summary>
    ValueTask<LifecycleExecutionHostExitTerminalizationResult> TerminalizeAsync (
        ResolvedUnityProjectContext project,
        LifecycleExecutionStartBinding start,
        ExecutionRef currentReference,
        LifecycleExecutionTerminalFacts observedTerminalFacts,
        LifecycleExecutionHostExitTerminalRecordFactory terminalRecordFactory);
}
