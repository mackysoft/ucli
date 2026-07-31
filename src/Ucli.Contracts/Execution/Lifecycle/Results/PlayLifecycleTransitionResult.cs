using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Represents the provider-independent typed result owned by one Play Mode Lifecycle Execution.
/// </summary>
public record PlayLifecycleTransitionResult
{
    /// <summary> Initializes one internally consistent Play Mode transition result. </summary>
    /// <param name="Transition"> The requested transition. </param>
    /// <param name="Result"> The transition outcome. </param>
    /// <param name="Before"> The lifecycle snapshot observed before the request. </param>
    /// <param name="After"> The lifecycle snapshot observed after a successful outcome. </param>
    /// <param name="Observed"> The latest lifecycle snapshot observed after a failed outcome. </param>
    /// <param name="ApplicationState"> Whether the failed request is known to have been applied. </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an enum argument is not defined by the public contract, or when a timeout does not report an indeterminate application state.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="Result" /> is incompatible with <paramref name="Transition" />, or success and failure fields are combined.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="Before" /> is <see langword="null" />, a successful outcome omits <paramref name="After" />, or a failed outcome omits <paramref name="Observed" />.
    /// </exception>
    [JsonConstructor]
    public PlayLifecycleTransitionResult (
        PlayLifecycleTransitionCommand Transition,
        PlayLifecycleTransitionOutcome Result,
        UnityEditorObservation Before,
        UnityEditorObservation? After,
        UnityEditorObservation? Observed,
        ExecutionApplicationState? ApplicationState)
    {
        if (!TextVocabulary.IsDefined(Transition))
        {
            throw new ArgumentOutOfRangeException(nameof(Transition), Transition, "Play transition command must be specified.");
        }

        if (!TextVocabulary.IsDefined(Result))
        {
            throw new ArgumentOutOfRangeException(nameof(Result), Result, "Play transition outcome must be specified.");
        }

        if (Before == null)
        {
            throw new ArgumentNullException(nameof(Before));
        }

        if (!IsCompatible(Transition, Result))
        {
            throw new ArgumentException($"Play transition outcome '{Result}' is incompatible with command '{Transition}'.", nameof(Result));
        }

        var isSuccess = IsSuccessfulOutcome(Result);
        if (isSuccess)
        {
            if (After == null)
            {
                throw new ArgumentNullException(nameof(After), "Successful Play transition must include an after snapshot.");
            }

            if (Observed != null)
            {
                throw new ArgumentException("Successful Play transition must not include an observed snapshot.", nameof(Observed));
            }

            if (ApplicationState.HasValue)
            {
                throw new ArgumentException("Successful Play transition must not include an application state.", nameof(ApplicationState));
            }
        }
        else
        {
            if (After != null)
            {
                throw new ArgumentException("Failed Play transition must not include an after snapshot.", nameof(After));
            }

            if (Observed == null)
            {
                throw new ArgumentNullException(nameof(Observed), "Failed Play transition must include an observed snapshot.");
            }

            if (!ApplicationState.HasValue)
            {
                throw new ArgumentException("Failed Play transition must specify an application state.", nameof(ApplicationState));
            }

            if (!TextVocabulary.IsDefined(ApplicationState.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(ApplicationState), ApplicationState, "Failed Play transition must specify an application state.");
            }
            if (ApplicationState.Value == ExecutionApplicationState.PartiallyApplied)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ApplicationState),
                    ApplicationState,
                    "Play Mode transitions do not support a partially applied state.");
            }

            if (Result == PlayLifecycleTransitionOutcome.Timeout
                && ApplicationState.Value != ExecutionApplicationState.Indeterminate)
            {
                throw new ArgumentOutOfRangeException(nameof(ApplicationState), ApplicationState, "Timed-out Play transition must have an indeterminate application state.");
            }
        }

        if (After != null
            && After.ProjectFingerprint != Before.ProjectFingerprint)
        {
            throw new ArgumentException(
                "Play transition after observation must belong to the same project as the before observation.",
                nameof(After));
        }
        if (Observed != null
            && Observed.ProjectFingerprint != Before.ProjectFingerprint)
        {
            throw new ArgumentException(
                "Play transition failure observation must belong to the same project as the before observation.",
                nameof(Observed));
        }

        this.Transition = Transition;
        this.Result = Result;
        this.Before = Before;
        this.After = After;
        this.Observed = Observed;
        this.ApplicationState = ApplicationState;
    }

    /// <summary> Gets the requested transition. </summary>
    public PlayLifecycleTransitionCommand Transition { get; }

    /// <summary> Gets the transition outcome. </summary>
    public PlayLifecycleTransitionOutcome Result { get; }

    /// <summary> Gets the lifecycle snapshot observed before issuing the transition request. </summary>
    public UnityEditorObservation Before { get; }

    /// <summary> Gets the lifecycle snapshot observed after a successful transition. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityEditorObservation? After { get; }

    /// <summary> Gets the latest lifecycle snapshot observed for transition errors or timeouts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityEditorObservation? Observed { get; }

    /// <summary> Gets the application state for transition errors or timeouts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExecutionApplicationState? ApplicationState { get; }

    /// <summary> Gets whether this result represents a completed transition. </summary>
    [JsonIgnore]
    public bool IsSuccessful => IsSuccessfulOutcome(Result);

    /// <summary>
    /// Gets the application state required when this outcome is retained by a terminal record or failure envelope.
    /// </summary>
    [JsonIgnore]
    public ExecutionApplicationState OutcomeApplicationState => Result switch
    {
        PlayLifecycleTransitionOutcome.Entered
            or PlayLifecycleTransitionOutcome.Exited =>
            ExecutionApplicationState.Applied,
        PlayLifecycleTransitionOutcome.AlreadyEntered
            or PlayLifecycleTransitionOutcome.AlreadyExited =>
            ExecutionApplicationState.NotApplied,
        PlayLifecycleTransitionOutcome.Timeout
            or PlayLifecycleTransitionOutcome.Blocked =>
            ApplicationState!.Value,
        _ => throw new ArgumentOutOfRangeException(
            nameof(Result),
            Result,
            "Play transition outcome must be specified."),
    };

    internal static bool IsCompatible (
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome result)
    {
        return (transition, result) switch
        {
            (PlayLifecycleTransitionCommand.Enter, PlayLifecycleTransitionOutcome.Entered or PlayLifecycleTransitionOutcome.AlreadyEntered) => true,
            (PlayLifecycleTransitionCommand.Exit, PlayLifecycleTransitionOutcome.Exited or PlayLifecycleTransitionOutcome.AlreadyExited) => true,
            (_, PlayLifecycleTransitionOutcome.Timeout or PlayLifecycleTransitionOutcome.Blocked) => true,
            _ => false,
        };
    }

    internal static bool IsSuccessfulOutcome (PlayLifecycleTransitionOutcome result)
    {
        return result is PlayLifecycleTransitionOutcome.Entered
            or PlayLifecycleTransitionOutcome.AlreadyEntered
            or PlayLifecycleTransitionOutcome.Exited
            or PlayLifecycleTransitionOutcome.AlreadyExited;
    }
}
