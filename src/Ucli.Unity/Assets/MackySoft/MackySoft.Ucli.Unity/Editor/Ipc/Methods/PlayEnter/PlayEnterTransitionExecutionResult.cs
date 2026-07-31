using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents a Unity-side Play Mode enter transition response and optional structured error. </summary>
    internal sealed record PlayEnterTransitionExecutionResult (
        PlayEnterTransitionExecutionResponse Response,
        PlayTransitionExecutionError Error)
    {
        /// <summary> Gets a value indicating whether the transition request succeeded. </summary>
        public bool IsSuccess => Error == null;

        /// <summary> Creates a successful transition result. </summary>
        /// <param name="response"> The structured transition response. </param>
        /// <returns> The successful result. </returns>
        public static PlayEnterTransitionExecutionResult Success (
            PlayEnterTransitionExecutionResponse response)
        {
            return new PlayEnterTransitionExecutionResult(response, null);
        }

        /// <summary> Creates a failed transition result. </summary>
        /// <param name="response"> The structured transition response. </param>
        /// <param name="error"> The structured transition error. </param>
        /// <returns> The failed result. </returns>
        public static PlayEnterTransitionExecutionResult Failure (
            PlayEnterTransitionExecutionResponse response,
            PlayTransitionExecutionError error)
        {
            return new PlayEnterTransitionExecutionResult(response, error);
        }
    }

    /// <summary>
    /// Carries the action-owned result before the common terminal record has been published.
    /// </summary>
    internal sealed record PlayEnterTransitionExecutionResponse (
        PlayLifecycleTransitionResult Transition);
}
