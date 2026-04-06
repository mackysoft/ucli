using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one Unity-side <c>test.run</c> service result. </summary>
    internal sealed record UnityTestRunServiceResult (
        IpcTestRunResponse? Payload,
        IpcError? Error)
    {
        /// <summary> Gets a value indicating whether the test run completed successfully. </summary>
        public bool IsSuccess => Payload != null && Error == null;

        /// <summary> Creates a successful service result. </summary>
        /// <param name="payload"> The successful response payload. </param>
        /// <returns> The successful service result. </returns>
        public static UnityTestRunServiceResult Success (IpcTestRunResponse payload)
        {
            return new UnityTestRunServiceResult(payload, null);
        }

        /// <summary> Creates a failed service result. </summary>
        /// <param name="error"> The lifecycle gate error. </param>
        /// <returns> The failed service result. </returns>
        public static UnityTestRunServiceResult Failure (IpcError error)
        {
            return new UnityTestRunServiceResult(null, error);
        }
    }
}
