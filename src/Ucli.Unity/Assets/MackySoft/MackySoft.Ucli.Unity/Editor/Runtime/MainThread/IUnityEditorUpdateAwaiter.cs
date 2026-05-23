using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Waits for Unity Editor update callbacks. </summary>
    internal interface IUnityEditorUpdateAwaiter
    {
        /// <summary> Completes after one Editor update callback. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> A task that completes on the next Editor update. </returns>
        Task WaitForNextUpdateAsync (CancellationToken cancellationToken);
    }
}
