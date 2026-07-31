using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json.Metadata;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play.Contracts;

/// <summary>
/// Represents the closed public evidence of a failed Play Mode transition.
/// </summary>
internal sealed record PlayTransitionFailureCommandOutput
{
    public PlayTransitionFailureCommandOutput (
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome result,
        PlayLifecycleSnapshotOutput before,
        PlayLifecycleSnapshotOutput observed,
        ExecutionApplicationState applicationState)
    {
        if (!TextVocabulary.IsDefined(transition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transition),
                transition,
                "Play Mode transition must be defined.");
        }
        if (result is not PlayLifecycleTransitionOutcome.Blocked
            and not PlayLifecycleTransitionOutcome.Timeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(result),
                result,
                "Play Mode transition failure output requires a blocked or timed-out outcome.");
        }
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(observed);
        if (applicationState == ExecutionApplicationState.PartiallyApplied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode transitions do not support a partially applied state.");
        }
        if (result == PlayLifecycleTransitionOutcome.Timeout
            && applicationState != ExecutionApplicationState.Indeterminate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "A timed-out Play Mode transition must have an indeterminate application state.");
        }

        Transition = transition;
        Result = result;
        Before = before;
        Observed = observed;
    }

    public PlayLifecycleTransitionCommand Transition { get; }

    [UcliPlayTransitionOutcomeSubset(
        UcliPlayTransitionOutcomeSubset.Failure)]
    public PlayLifecycleTransitionOutcome Result { get; }

    public PlayLifecycleSnapshotOutput Before { get; }

    public PlayLifecycleSnapshotOutput Observed { get; }
}
