using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Represents the Play Mode exit result retained by a Lifecycle Execution Terminal Record.
/// </summary>
public sealed record PlayExitLifecycleTransitionResult
    : PlayLifecycleTransitionResult
{
    /// <summary> Initializes one Play Mode exit result. </summary>
    /// <param name="Transition"> The requested transition, which must be Play Mode exit. </param>
    /// <param name="Result"> The exit-compatible transition outcome. </param>
    /// <param name="Before"> The lifecycle snapshot observed before the request. </param>
    /// <param name="After"> The lifecycle snapshot observed after a successful outcome. </param>
    /// <param name="Observed"> The latest lifecycle snapshot observed after a failed outcome. </param>
    /// <param name="ApplicationState"> Whether the failed request is known to have been applied. </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="Transition" /> is not Play Mode exit, or the result fields are inconsistent.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when an observation required by the result is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an enum argument is not defined by the public contract.
    /// </exception>
    [JsonConstructor]
    public PlayExitLifecycleTransitionResult (
        PlayLifecycleTransitionCommand Transition,
        PlayLifecycleTransitionOutcome Result,
        UnityEditorObservation Before,
        UnityEditorObservation? After,
        UnityEditorObservation? Observed,
        ExecutionApplicationState? ApplicationState)
        : base(
            Transition,
            Result,
            Before,
            After,
            Observed,
            ApplicationState)
    {
        if (Transition != PlayLifecycleTransitionCommand.Exit)
        {
            throw new ArgumentException(
                "Play Mode exit result requires an exit transition.",
                nameof(Transition));
        }
    }

    /// <summary>
    /// Normalizes one provider-independent Play transition result for Terminal Record publication.
    /// </summary>
    /// <param name="result"> The provider-independent result to normalize. </param>
    /// <returns> The action-specific Play Mode exit result. </returns>
    /// <exception cref="ArgumentNullException"> <paramref name="result" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> <paramref name="result" /> is not an exit-compatible result. </exception>
    public static PlayExitLifecycleTransitionResult FromProviderResult (
        PlayLifecycleTransitionResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        return new PlayExitLifecycleTransitionResult(
            result.Transition,
            result.Result,
            result.Before,
            result.After,
            result.Observed,
            result.ApplicationState);
    }
}
