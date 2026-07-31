using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents a Unity-side Play Mode exit transition result and optional structured error. </summary>
    internal sealed record PlayExitTransitionExecutionResult (
        PlayLifecycleTransitionResult Result,
        PlayTransitionExecutionError Error)
    {
        /// <summary> Gets a value indicating whether the transition request succeeded. </summary>
        public bool IsSuccess => Error == null;

        /// <summary> Creates a successful transition result. </summary>
        /// <param name="result"> The typed transition result. </param>
        /// <returns> The successful result. </returns>
        public static PlayExitTransitionExecutionResult Success (PlayLifecycleTransitionResult result)
        {
            return new PlayExitTransitionExecutionResult(result, null);
        }

        /// <summary> Creates a failed transition result. </summary>
        /// <param name="result"> The typed transition result. </param>
        /// <param name="error"> The structured transition error. </param>
        /// <returns> The failed result. </returns>
        public static PlayExitTransitionExecutionResult Failure (
            PlayLifecycleTransitionResult result,
            PlayTransitionExecutionError error)
        {
            return new PlayExitTransitionExecutionResult(result, error);
        }
    }
}
