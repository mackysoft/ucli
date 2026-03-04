using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Creates normalized request context values for daemon <c>test.run</c> execution. </summary>
    internal interface IUnityTestRunRequestContextFactory
    {
        /// <summary> Creates one normalized request context from IPC payload values. </summary>
        /// <param name="request"> The decoded IPC request payload. </param>
        /// <returns> The normalized request context. </returns>
        UnityTestRunRequestContext Create (IpcTestRunRequest request);
    }
}
