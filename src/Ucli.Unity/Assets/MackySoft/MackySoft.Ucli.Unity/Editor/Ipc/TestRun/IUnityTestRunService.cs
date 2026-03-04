using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes Unity Test Framework runs for daemon <c>test.run</c> requests. </summary>
    internal interface IUnityTestRunService
    {
        /// <summary> Executes one daemon <c>test.run</c> request and returns IPC response payload. </summary>
        /// <param name="request"> The decoded request payload. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The response payload. </returns>
        Task<IpcTestRunResponse> Execute (
            IpcTestRunRequest request,
            CancellationToken cancellationToken = default);
    }
}
