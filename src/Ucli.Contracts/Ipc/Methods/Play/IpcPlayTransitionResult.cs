using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the result of one Play Mode lifecycle transition request. </summary>
public sealed record IpcPlayTransitionResult
{
    /// <summary> Initializes one internally consistent Play Mode transition result. </summary>
    /// <param name="Transition"> The requested transition. </param>
    /// <param name="Result"> The transition outcome. </param>
    /// <param name="Before"> The lifecycle snapshot observed before the request. </param>
    /// <param name="After"> The lifecycle snapshot observed after a successful outcome. </param>
    /// <param name="Observed"> The latest lifecycle snapshot observed after a failed outcome. </param>
    /// <param name="ApplicationState"> Whether the failed request is known to have been applied. </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an enum argument is not defined by the wire contract, or when a timeout does not report an indeterminate application state.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="Result" /> is incompatible with <paramref name="Transition" />, or success and failure fields are combined.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="Before" /> is <see langword="null" />, a successful outcome omits <paramref name="After" />, or a failed outcome omits <paramref name="Observed" />.
    /// </exception>
    [JsonConstructor]
    public IpcPlayTransitionResult (
        IpcPlayTransitionCommand Transition,
        IpcPlayTransitionOutcome Result,
        IpcUnityEditorObservation Before,
        IpcUnityEditorObservation? After,
        IpcUnityEditorObservation? Observed,
        IpcApplicationState? ApplicationState)
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

        var isSuccess = Result is IpcPlayTransitionOutcome.Entered
            or IpcPlayTransitionOutcome.AlreadyEntered
            or IpcPlayTransitionOutcome.Exited
            or IpcPlayTransitionOutcome.AlreadyExited;
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

            if (Result == IpcPlayTransitionOutcome.Timeout
                && ApplicationState.Value != IpcApplicationState.Indeterminate)
            {
                throw new ArgumentOutOfRangeException(nameof(ApplicationState), ApplicationState, "Timed-out Play transition must have an indeterminate application state.");
            }
        }

        this.Transition = Transition;
        this.Result = Result;
        this.Before = Before;
        this.After = After;
        this.Observed = Observed;
        this.ApplicationState = ApplicationState;
    }

    /// <summary> Gets the requested transition. </summary>
    public IpcPlayTransitionCommand Transition { get; }

    /// <summary> Gets the transition outcome. </summary>
    public IpcPlayTransitionOutcome Result { get; }

    /// <summary> Gets the lifecycle snapshot observed before issuing the transition request. </summary>
    public IpcUnityEditorObservation Before { get; }

    /// <summary> Gets the lifecycle snapshot observed after a successful transition. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcUnityEditorObservation? After { get; }

    /// <summary> Gets the latest lifecycle snapshot observed for transition errors or timeouts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcUnityEditorObservation? Observed { get; }

    /// <summary> Gets the application state for transition errors or timeouts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcApplicationState? ApplicationState { get; }

    private static bool IsCompatible (
        IpcPlayTransitionCommand transition,
        IpcPlayTransitionOutcome result)
    {
        return (transition, result) switch
        {
            (IpcPlayTransitionCommand.Enter, IpcPlayTransitionOutcome.Entered or IpcPlayTransitionOutcome.AlreadyEntered) => true,
            (IpcPlayTransitionCommand.Exit, IpcPlayTransitionOutcome.Exited or IpcPlayTransitionOutcome.AlreadyExited) => true,
            (_, IpcPlayTransitionOutcome.Timeout or IpcPlayTransitionOutcome.Blocked) => true,
            _ => false,
        };
    }
}
