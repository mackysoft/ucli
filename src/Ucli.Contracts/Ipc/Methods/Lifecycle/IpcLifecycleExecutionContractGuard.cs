using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Enforces Lifecycle Execution references carried by typed IPC delivery contracts. </summary>
internal static class IpcLifecycleExecutionContractGuard
{
    public static LifecycleExecutionStartBinding RequireStart (
        LifecycleExecutionStartBinding start,
        LifecycleExecutionKind expectedKind,
        string parameterName)
    {
        if (start == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        LifecycleExecutionContractGuard.RequireReference(
            start.LifecycleExecutionRef,
            parameterName,
            expectedKind,
            allowTerminal: false);
        return start;
    }

    public static ExecutionRef RequireSuccessfulReference (
        ExecutionRef lifecycleExecutionRef,
        LifecycleExecutionKind expectedKind,
        string parameterName)
    {
        return LifecycleExecutionContractGuard.RequireCompletedTerminalReference(
            lifecycleExecutionRef,
            parameterName,
            expectedKind);
    }

    public static ExecutionRef? RequireErrorReference (
        ExecutionRef? lifecycleExecutionRef,
        LifecycleExecutionKind expectedKind,
        string parameterName)
    {
        if (lifecycleExecutionRef == null)
        {
            return null;
        }

        return LifecycleExecutionContractGuard.RequireFailureReference(
            lifecycleExecutionRef,
            parameterName,
            expectedKind);
    }

    public static ExecutionApplicationState RequireApplicationState (
        ExecutionApplicationState applicationState,
        string parameterName)
    {
        if (!TextVocabulary.IsDefined(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                applicationState,
                "Lifecycle Execution application state must be defined.");
        }

        return applicationState;
    }
}
