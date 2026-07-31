using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Resolves terminal facts that are independent of an action's state machine and typed result.
/// </summary>
internal static class LifecycleExecutionTerminalFactsPolicy
{
    /// <summary>
    /// Resolves what can be asserted when no action-specific completion evidence exists.
    /// </summary>
    internal static ExecutionApplicationState ResolveUnprovenApplicationState (
        ExecutionRef? currentReference,
        bool lifecycleActionAdmitted)
    {
        return !lifecycleActionAdmitted
                && currentReference?.Lifecycle == ExecutionLifecycle.Active
                && string.Equals(
                    currentReference.State.Value,
                    TextVocabulary.GetText(
                        LifecycleExecutionState.Registered),
                    StringComparison.Ordinal)
            ? ExecutionApplicationState.NotApplied
            : ExecutionApplicationState.Indeterminate;
    }

    /// <summary>
    /// Resolves the common facts established by an exact fixed-host exit observation.
    /// </summary>
    internal static LifecycleExecutionTerminalFacts ResolveHostExit (
        LifecycleExecutionStartBinding start,
        ExecutionRef currentReference,
        bool lifecycleActionAdmitted,
        DateTimeOffset observedAtUtc)
    {
        if (start is null)
        {
            throw new ArgumentNullException(nameof(start));
        }

        if (currentReference is null)
        {
            throw new ArgumentNullException(nameof(currentReference));
        }

        return ResolveTerminalFacts(
            start,
            LifecycleExecutionTerminalReason.UnityExited,
            ResolveUnprovenApplicationState(
                currentReference,
                lifecycleActionAdmitted),
            terminalGeneration: null,
            completedAtUtc: observedAtUtc);
    }

    /// <summary>
    /// Resolves action-independent terminal facts immediately before a terminal candidate is
    /// fixed.
    /// </summary>
    /// <remarks>
    /// Action-owned results, verdicts, evidence, and error messages remain the responsibility of
    /// the action handler.
    /// </remarks>
    internal static LifecycleExecutionTerminalFacts ResolveTerminalFacts (
        LifecycleExecutionStartBinding start,
        LifecycleExecutionTerminalReason terminalReason,
        ExecutionApplicationState applicationState,
        UnityEditorGenerationSnapshot? terminalGeneration,
        DateTimeOffset completedAtUtc)
    {
        if (start is null)
        {
            throw new ArgumentNullException(nameof(start));
        }
        if (!TextVocabulary.IsDefined(terminalReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminalReason),
                terminalReason,
                "Lifecycle Execution terminal reason must be defined.");
        }
        if (!TextVocabulary.IsDefined(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Lifecycle Execution application state must be defined.");
        }

        var normalizedCompletedAtUtc = EnsureNotBefore(
            completedAtUtc,
            start.StartedAtUtc);
        var resolvedTerminalReason = terminalReason;
        var resolvedTerminalGeneration = terminalGeneration;

        if (!CanAttributeObservedGeneration(terminalReason))
        {
            resolvedTerminalGeneration = null;
        }
        else if (terminalGeneration is not null
            && !LifecycleExecutionGenerationRules.IsMonotonicSuccessor(
                start.StartedGeneration,
                terminalGeneration))
        {
            resolvedTerminalReason =
                LifecycleExecutionTerminalReason.GenerationMismatch;
            resolvedTerminalGeneration = null;
        }

        if (normalizedCompletedAtUtc >= start.DeadlineUtc)
        {
            resolvedTerminalReason =
                LifecycleExecutionTerminalReason.DeadlineExceeded;
        }
        else if (resolvedTerminalReason
            == LifecycleExecutionTerminalReason.DeadlineExceeded)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAtUtc),
                completedAtUtc,
                "A deadline-exceeded Lifecycle Execution cannot complete before its durable deadline.");
        }

        return new LifecycleExecutionTerminalFacts(
            resolvedTerminalReason,
            applicationState,
            resolvedTerminalGeneration,
            normalizedCompletedAtUtc);
    }

    /// <summary>
    /// Returns whether an observation made at <paramref name="observedAtUtc" /> has reached the
    /// execution's immutable deadline.
    /// </summary>
    internal static bool HasReachedDeadline (
        LifecycleExecutionStartBinding start,
        DateTimeOffset observedAtUtc)
    {
        if (start is null)
        {
            throw new ArgumentNullException(nameof(start));
        }

        return observedAtUtc.ToUniversalTime() >= start.DeadlineUtc;
    }

    /// <summary>
    /// Clamps a completion time to a durable lower bound and normalizes it to UTC.
    /// </summary>
    internal static DateTimeOffset EnsureNotBefore (
        DateTimeOffset observedAtUtc,
        DateTimeOffset lowerBoundUtc)
    {
        var normalizedObservedAtUtc = observedAtUtc.ToUniversalTime();
        var normalizedLowerBoundUtc = lowerBoundUtc.ToUniversalTime();
        return normalizedObservedAtUtc < normalizedLowerBoundUtc
            ? normalizedLowerBoundUtc
            : normalizedObservedAtUtc;
    }

    /// <summary>
    /// Returns whether the current host may attribute an observed terminal generation to the
    /// fixed execution after the supplied terminal reason.
    /// </summary>
    internal static bool CanAttributeObservedGeneration (
        LifecycleExecutionTerminalReason terminalReason)
    {
        return terminalReason
            is not LifecycleExecutionTerminalReason.ProjectMismatch
            and not LifecycleExecutionTerminalReason.HostMismatch
            and not LifecycleExecutionTerminalReason.GenerationMismatch
            and not LifecycleExecutionTerminalReason.UnityExited;
    }
}
