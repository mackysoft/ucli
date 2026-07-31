using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json.Metadata;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary>
/// Represents the closed public evidence of one successful Play Mode transition.
/// </summary>
internal sealed record PlayTransitionSuccessOutput
{
    public PlayTransitionSuccessOutput (
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome result,
        PlayLifecycleSnapshotOutput before,
        PlayLifecycleSnapshotOutput after)
    {
        if (!TextVocabulary.IsDefined(transition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transition),
                transition,
                "Play Mode transition must be defined.");
        }
        if (!TextVocabulary.IsDefined(result)
            || result is not PlayLifecycleTransitionOutcome.Entered
                and not PlayLifecycleTransitionOutcome.AlreadyEntered
                and not PlayLifecycleTransitionOutcome.Exited
                and not PlayLifecycleTransitionOutcome.AlreadyExited)
        {
            throw new ArgumentOutOfRangeException(
                nameof(result),
                result,
                "Play Mode success output requires a successful transition outcome.");
        }
        if ((transition == PlayLifecycleTransitionCommand.Enter
                && result is not PlayLifecycleTransitionOutcome.Entered
                    and not PlayLifecycleTransitionOutcome.AlreadyEntered)
            || (transition == PlayLifecycleTransitionCommand.Exit
                && result is not PlayLifecycleTransitionOutcome.Exited
                    and not PlayLifecycleTransitionOutcome.AlreadyExited))
        {
            throw new ArgumentException(
                "Play Mode success outcome must match its requested transition.",
                nameof(result));
        }

        Transition = transition;
        Result = result;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
    }

    public PlayLifecycleTransitionCommand Transition { get; }

    [UcliPlayTransitionOutcomeSubset(
        UcliPlayTransitionOutcomeSubset.Success)]
    public PlayLifecycleTransitionOutcome Result { get; }

    public PlayLifecycleSnapshotOutput Before { get; }

    public PlayLifecycleSnapshotOutput After { get; }
}
