namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the blocking reason and normal-request acceptance tuple required by each editor lifecycle state. </summary>
public static class IpcEditorLifecycleSemantics
{
    /// <summary> Resolves the blocking reason required by a defined editor lifecycle state. </summary>
    /// <param name="lifecycleState"> The lifecycle state to resolve. </param>
    /// <returns> The required blocking reason, or <see langword="null" /> for the ready state. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="lifecycleState" /> is not defined or mapped. </exception>
    public static IpcEditorBlockingReason? ResolveBlockingReason (IpcEditorLifecycleState lifecycleState)
    {
        if (TryResolve(lifecycleState, out var blockingReason, out _))
        {
            return blockingReason;
        }

        throw new ArgumentOutOfRangeException(nameof(lifecycleState), lifecycleState, "Unsupported editor lifecycle state.");
    }

    /// <summary> Determines whether a defined editor lifecycle state permits normal execution requests. </summary>
    /// <param name="lifecycleState"> The lifecycle state to resolve. </param>
    /// <returns> <see langword="true" /> only for a lifecycle state that permits normal execution requests. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="lifecycleState" /> is not defined or mapped. </exception>
    public static bool CanAcceptExecutionRequests (IpcEditorLifecycleState lifecycleState)
    {
        if (TryResolve(lifecycleState, out _, out var canAcceptExecutionRequests))
        {
            return canAcceptExecutionRequests;
        }

        throw new ArgumentOutOfRangeException(nameof(lifecycleState), lifecycleState, "Unsupported editor lifecycle state.");
    }

    /// <summary> Determines whether a typed lifecycle tuple matches the values required by its lifecycle state. </summary>
    /// <param name="lifecycleState"> The lifecycle state to validate. </param>
    /// <param name="blockingReason"> The blocking reason to validate. </param>
    /// <param name="canAcceptExecutionRequests"> The normal-request acceptance value to validate. </param>
    /// <returns> <see langword="true" /> when the state is defined and both associated values match; otherwise <see langword="false" />. </returns>
    public static bool IsConsistent (
        IpcEditorLifecycleState lifecycleState,
        IpcEditorBlockingReason? blockingReason,
        bool canAcceptExecutionRequests)
    {
        return TryResolve(
                lifecycleState,
                out var expectedBlockingReason,
                out var expectedCanAcceptExecutionRequests)
            && blockingReason == expectedBlockingReason
            && canAcceptExecutionRequests == expectedCanAcceptExecutionRequests;
    }

    private static bool TryResolve (
        IpcEditorLifecycleState lifecycleState,
        out IpcEditorBlockingReason? blockingReason,
        out bool canAcceptExecutionRequests)
    {
        if (lifecycleState == IpcEditorLifecycleState.Ready)
        {
            blockingReason = null;
            canAcceptExecutionRequests = true;
            return true;
        }

        blockingReason = lifecycleState switch
        {
            IpcEditorLifecycleState.Starting => IpcEditorBlockingReason.Startup,
            IpcEditorLifecycleState.Recovering => IpcEditorBlockingReason.Recovery,
            IpcEditorLifecycleState.Busy => IpcEditorBlockingReason.Busy,
            IpcEditorLifecycleState.Compiling => IpcEditorBlockingReason.Compile,
            IpcEditorLifecycleState.CompileFailed => IpcEditorBlockingReason.CompileFailed,
            IpcEditorLifecycleState.DomainReloading => IpcEditorBlockingReason.DomainReload,
            IpcEditorLifecycleState.Reimporting => IpcEditorBlockingReason.Reimport,
            IpcEditorLifecycleState.PlayMode => IpcEditorBlockingReason.PlayMode,
            IpcEditorLifecycleState.ModalBlocked => IpcEditorBlockingReason.ModalDialog,
            IpcEditorLifecycleState.SafeMode => IpcEditorBlockingReason.SafeMode,
            IpcEditorLifecycleState.ShuttingDown => IpcEditorBlockingReason.Shutdown,
            IpcEditorLifecycleState.Unavailable => IpcEditorBlockingReason.Unavailable,
            _ => null,
        };
        canAcceptExecutionRequests = false;
        return blockingReason.HasValue;
    }
}
