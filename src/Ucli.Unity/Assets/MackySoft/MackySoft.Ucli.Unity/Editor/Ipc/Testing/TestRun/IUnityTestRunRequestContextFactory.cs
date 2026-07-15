using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Creates normalized request context values and host-owned artifact paths for daemon <c>test.run</c> execution. </summary>
    internal interface IUnityTestRunRequestContextFactory
    {
        /// <summary> Creates one normalized request context whose artifact paths are derived from the current host identity and request run identifier. </summary>
        /// <param name="request"> The decoded IPC request payload. </param>
        /// <returns> The normalized request context. </returns>
        UnityTestRunRequestContext Create (IpcTestRunRequest request);
    }
}
