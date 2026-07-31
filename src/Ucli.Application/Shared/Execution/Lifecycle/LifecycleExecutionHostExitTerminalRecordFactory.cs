using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Creates the action-owned Terminal Record from common facts established for one fixed-host exit.
/// </summary>
internal delegate LifecycleExecutionTerminalRecord
    LifecycleExecutionHostExitTerminalRecordFactory (
        LifecycleExecutionStartBinding start,
        LifecycleExecutionTerminalFacts terminalFacts);
