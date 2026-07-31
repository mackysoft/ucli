using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Carries the execution reference and application state that remain proven when one caller stops
/// waiting for a Lifecycle Execution response.
/// </summary>
internal sealed record LifecycleExecutionWaitFailure (
    ExecutionRef? ExecutionReference,
    ExecutionApplicationState ApplicationState)
{
    /// <summary>
    /// Resolves the continuation retained after a new or reconnected execution wait fails.
    /// </summary>
    /// <param name="durableStartExecutionReference">
    /// The execution reference proven by a durable Start Record during this wait, or
    /// <see langword="null" /> when no new Start Record was established.
    /// </param>
    /// <param name="isCallerCancellation">
    /// Whether this caller stopped waiting through cancellation.
    /// </param>
    /// <param name="lifecycleActionDispatched">
    /// Whether provider dispatch was confirmed after the durable start.
    /// </param>
    /// <param name="establishedExecutionReference">
    /// The authoritative execution reference used to reconnect, or <see langword="null" />
    /// for a new execution.
    /// </param>
    /// <returns>
    /// The execution reference and application state that remain proven after the wait fails.
    /// </returns>
    public static LifecycleExecutionWaitFailure Resolve (
        ExecutionRef? durableStartExecutionReference,
        bool isCallerCancellation,
        bool lifecycleActionDispatched,
        ExecutionRef? establishedExecutionReference)
    {
        var applicationState = establishedExecutionReference is null
            ? ResolveNewExecutionState(
                durableStartExecutionReference,
                isCallerCancellation,
                lifecycleActionDispatched)
            : ResolveReconnectedExecutionState(
                isCallerCancellation,
                lifecycleActionDispatched);
        return new LifecycleExecutionWaitFailure(
            establishedExecutionReference
                ?? durableStartExecutionReference,
            applicationState);
    }

    private static ExecutionApplicationState ResolveNewExecutionState (
        ExecutionRef? durableStartExecutionReference,
        bool isCallerCancellation,
        bool lifecycleActionDispatched)
    {
        if (durableStartExecutionReference is null)
        {
            return ExecutionApplicationState.NotApplied;
        }

        // Once Start is durable, caller cancellation races with the background action dispatch.
        // A false observation here cannot prove that the action will remain undispatched.
        return isCallerCancellation
                || lifecycleActionDispatched
            ? ExecutionApplicationState.Indeterminate
            : ExecutionApplicationState.NotApplied;
    }

    private static ExecutionApplicationState ResolveReconnectedExecutionState (
        bool isCallerCancellation,
        bool lifecycleActionDispatched)
    {
        // A canceled wait races the provider-owned dispatch, and a confirmed dispatch proves
        // that application may already be in progress. Otherwise this reconnect attempt cannot
        // establish whether the original action was ever applied.
        return isCallerCancellation
                || lifecycleActionDispatched
            ? ExecutionApplicationState.Indeterminate
            : ExecutionApplicationState.Unknown;
    }
}
