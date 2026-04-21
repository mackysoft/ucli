using System;
using System.Threading;
using System.Threading.Tasks;
namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Executes asynchronous work items on Unity main thread context. </summary>
    internal interface IUnityMainThreadRequestExecutor
    {
        /// <summary> Executes one asynchronous work item on Unity main thread. </summary>
        /// <typeparam name="T"> The work-item result type. </typeparam>
        /// <param name="workItem"> The asynchronous work item to execute. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by connection handling. </param>
        /// <returns> The work-item result. </returns>
        Task<T> Execute<T> (
            Func<Task<T>> workItem,
            CancellationToken cancellationToken = default);
    }
}
