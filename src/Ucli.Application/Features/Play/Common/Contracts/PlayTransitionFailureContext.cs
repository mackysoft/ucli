using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary>
/// Retains the durable execution identity and confirmed application state when waiting for a
/// Play Mode Lifecycle Execution does not produce an action result.
/// </summary>
internal sealed record PlayTransitionFailureContext
{
    /// <summary> Initializes one registered Play Mode failure context. </summary>
    public PlayTransitionFailureContext (
        ProjectIdentityInfo project,
        ExecutionRef lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        PlayLifecycleSnapshotOutput? currentLifecycle = null,
        PlayTransitionOutput? transition = null,
        int? timeoutMilliseconds = null)
    {
        if (!TextVocabulary.IsDefined(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode application state must be defined.");
        }
        if (applicationState == ExecutionApplicationState.PartiallyApplied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode transitions do not support a partially applied state.");
        }
        if ((currentLifecycle == null) != (transition == null)
            || (transition == null) != (timeoutMilliseconds == null))
        {
            throw new ArgumentException(
                "A typed Play Mode failure projection must include lifecycle, transition, and timeout together.",
                nameof(transition));
        }
        if (timeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMilliseconds),
                timeoutMilliseconds,
                "Play Mode timeout must be positive when a typed transition is retained.");
        }

        Project = project ?? throw new ArgumentNullException(nameof(project));
        LifecycleExecutionRef = lifecycleExecutionRef
            ?? throw new ArgumentNullException(nameof(lifecycleExecutionRef));
        ApplicationState = applicationState;
        CurrentLifecycle = currentLifecycle;
        Transition = transition;
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    /// <summary> Gets the project whose Lifecycle Execution was registered. </summary>
    public ProjectIdentityInfo Project { get; }

    /// <summary> Gets the active, recovery, or failed terminal Lifecycle Execution reference. </summary>
    public ExecutionRef LifecycleExecutionRef { get; }

    /// <summary> Gets the application state confirmed at the failed wait boundary. </summary>
    public ExecutionApplicationState ApplicationState { get; }

    /// <summary> Gets the last projected Editor lifecycle when an action result was established. </summary>
    public PlayLifecycleSnapshotOutput? CurrentLifecycle { get; }

    /// <summary> Gets the typed transition result established before delivery failed. </summary>
    public PlayTransitionOutput? Transition { get; }

    /// <summary> Gets the effective action timeout when a typed transition was established. </summary>
    public int? TimeoutMilliseconds { get; }
}
