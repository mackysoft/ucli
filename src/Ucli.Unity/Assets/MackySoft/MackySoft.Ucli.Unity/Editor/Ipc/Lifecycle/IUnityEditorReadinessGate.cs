using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Waits until the Unity editor is ready to accept editor-mutating operations. </summary>
    internal interface IUnityEditorReadinessGate
    {
        /// <summary> Waits until compilation and asset updating have both completed. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        Task WaitUntilReady (CancellationToken cancellationToken = default);
    }
}