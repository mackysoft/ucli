using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Defines the filesystem boundary used by one GUI lifecycle sidecar writer generation. </summary>
    internal interface IUnityLifecycleSidecarPersistence
    {
        /// <summary> Persists one immutable lifecycle snapshot. </summary>
        Task WriteAsync (
            UnityEditorLifecycleSnapshot snapshot,
            CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the persisted sidecar only when its contents still belong to this persistence generation.
        /// </summary>
        Task DeleteIfOwnedAsync (CancellationToken cancellationToken);
    }
}
