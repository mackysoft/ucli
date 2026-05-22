using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents a Unity-side Play Mode enter transition response and optional structured error. </summary>
    internal sealed record PlayEnterTransitionExecutionResult (
        IpcPlayTransitionResponse Response,
        IpcError Error)
    {
        /// <summary> Gets a value indicating whether the transition request succeeded. </summary>
        public bool IsSuccess => Error == null;

        /// <summary> Creates a successful transition result. </summary>
        /// <param name="response"> The structured transition response. </param>
        /// <returns> The successful result. </returns>
        public static PlayEnterTransitionExecutionResult Success (IpcPlayTransitionResponse response)
        {
            return new PlayEnterTransitionExecutionResult(response, null);
        }

        /// <summary> Creates a failed transition result. </summary>
        /// <param name="response"> The structured transition response. </param>
        /// <param name="error"> The structured transition error. </param>
        /// <returns> The failed result. </returns>
        public static PlayEnterTransitionExecutionResult Failure (
            IpcPlayTransitionResponse response,
            IpcError error)
        {
            return new PlayEnterTransitionExecutionResult(response, error);
        }
    }
}
