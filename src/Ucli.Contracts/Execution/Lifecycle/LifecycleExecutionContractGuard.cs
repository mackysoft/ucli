namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Enforces the closed Lifecycle Execution projection carried by common execution references. </summary>
internal static class LifecycleExecutionContractGuard
{
    /// <summary>
    /// Requires the completed terminal reference published by one successful Lifecycle Execution.
    /// </summary>
    public static ITerminalExecutionRef RequireCompletedTerminalReference (
        ITerminalExecutionRef lifecycleExecutionRef,
        string parameterName,
        LifecycleExecutionKind expectedKind)
    {
        if (lifecycleExecutionRef == null)
        {
            throw new ArgumentNullException(parameterName);
        }
        if (lifecycleExecutionRef is not ExecutionRef executionRef)
        {
            throw new ArgumentException(
                "Lifecycle Execution output requires the canonical terminal execution-reference contract.",
                parameterName);
        }

        RequireCompletedTerminalReference(
            executionRef,
            parameterName,
            expectedKind);
        return lifecycleExecutionRef;
    }

    /// <summary>
    /// Requires the completed terminal reference published by one successful Lifecycle Execution.
    /// </summary>
    public static ExecutionRef RequireCompletedTerminalReference (
        ExecutionRef lifecycleExecutionRef,
        string parameterName,
        LifecycleExecutionKind expectedKind)
    {
        RequireReference(
            lifecycleExecutionRef,
            parameterName,
            expectedKind);
        if (lifecycleExecutionRef.Lifecycle != ExecutionLifecycle.Terminal
            || lifecycleExecutionRef.State.Value
                != TextVocabulary.GetText(LifecycleExecutionState.Completed))
        {
            throw new ArgumentException(
                "Successful Lifecycle Execution output requires a completed terminal reference.",
                parameterName);
        }

        return lifecycleExecutionRef;
    }

    /// <summary>
    /// Requires a reconnectable reference or the failed terminal reference published with a Lifecycle Execution failure.
    /// </summary>
    public static ExecutionRef RequireFailureReference (
        ExecutionRef lifecycleExecutionRef,
        string parameterName,
        LifecycleExecutionKind expectedKind)
    {
        RequireReference(
            lifecycleExecutionRef,
            parameterName,
            expectedKind);
        if (lifecycleExecutionRef.Lifecycle == ExecutionLifecycle.Terminal
            && lifecycleExecutionRef.State.Value
                != TextVocabulary.GetText(LifecycleExecutionState.Failed))
        {
            throw new ArgumentException(
                "Lifecycle Execution failure output may carry only a failed terminal reference.",
                parameterName);
        }

        return lifecycleExecutionRef;
    }

    public static LifecycleExecutionKind RequireReference (
        ExecutionRef executionRef,
        string parameterName,
        LifecycleExecutionKind? expectedKind = null,
        bool allowTerminal = true)
    {
        if (executionRef == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (!TextVocabulary.TryGetValue(
            executionRef.Kind.Value,
            out LifecycleExecutionKind actualKind))
        {
            throw new ArgumentException(
                "Execution reference kind is not a Lifecycle Execution kind.",
                parameterName);
        }

        if (expectedKind.HasValue && actualKind != expectedKind.Value)
        {
            throw new ArgumentException(
                $"Execution reference kind must be '{TextVocabulary.GetText(expectedKind.Value)}'.",
                parameterName);
        }

        var expectedDigest = LifecycleExecutionDefinitionDigest.Calculate(
            new LifecycleExecutionDefinition(actualKind));
        if (executionRef.DefinitionDigest != expectedDigest)
        {
            throw new ArgumentException(
                "Execution reference definition digest does not match its fixed Lifecycle Execution definition.",
                parameterName);
        }

        if (!TextVocabulary.TryGetValue(
            executionRef.State.Value,
            out LifecycleExecutionState actualState)
            || !IsStateAllowed(actualKind, executionRef.Lifecycle, actualState))
        {
            throw new ArgumentException(
                "Execution reference state is not valid for its Lifecycle Execution kind and lifecycle.",
                parameterName);
        }

        if (!allowTerminal && executionRef.Lifecycle == ExecutionLifecycle.Terminal)
        {
            throw new ArgumentException(
                "Lifecycle Execution start binding must carry an active or recovery execution reference.",
                parameterName);
        }

        if (executionRef.Lifecycle != ExecutionLifecycle.Terminal
            && executionRef.StatusLocator == null)
        {
            throw new ArgumentException(
                "Active and recovery Lifecycle Execution references must resolve to durable status.",
                parameterName);
        }

        if (executionRef.Lifecycle == ExecutionLifecycle.Terminal)
        {
            var terminalReference = (ITerminalExecutionRef)executionRef;
            if (terminalReference.TerminalRecordRef.Kind
                    != LifecycleExecutionArtifactContract.TerminalRecordKind
                || terminalReference.TerminalRecordRef.MediaType
                    != LifecycleExecutionArtifactContract.TerminalRecordMediaType)
            {
                throw new ArgumentException(
                    "Terminal Lifecycle Execution references must resolve to a Lifecycle Execution Terminal Record.",
                    parameterName);
            }
        }

        return actualKind;
    }

    internal static bool IsStateAllowed (
        LifecycleExecutionKind kind,
        ExecutionLifecycle lifecycle,
        LifecycleExecutionState state)
    {
        return lifecycle switch
        {
            ExecutionLifecycle.Active => (kind, state) switch
            {
                (LifecycleExecutionKind.Refresh,
                    LifecycleExecutionState.Registered or LifecycleExecutionState.Refreshing) => true,
                (LifecycleExecutionKind.Compile,
                    LifecycleExecutionState.Registered
                    or LifecycleExecutionState.Refreshing
                    or LifecycleExecutionState.Compiling) => true,
                (LifecycleExecutionKind.PlayEnter,
                    LifecycleExecutionState.Registered or LifecycleExecutionState.Entering) => true,
                (LifecycleExecutionKind.PlayExit,
                    LifecycleExecutionState.Registered or LifecycleExecutionState.Exiting) => true,
                _ => false,
            },
            ExecutionLifecycle.Recovery => state is LifecycleExecutionState.Recovering
                or LifecycleExecutionState.Publishing,
            ExecutionLifecycle.Terminal => state is LifecycleExecutionState.Completed
                or LifecycleExecutionState.Failed,
            _ => false,
        };
    }
}
