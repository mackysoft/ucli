using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Captures editor lifecycle telemetry and gates execution requests. </summary>
    internal interface IUnityEditorReadinessGate
    {
        /// <summary> Captures the current editor lifecycle snapshot. </summary>
        /// <returns> The normalized lifecycle snapshot. </returns>
        UnityEditorLifecycleSnapshot CaptureSnapshot ();

        /// <summary> Ensures the editor can accept execution requests, optionally waiting for readiness. </summary>
        /// <param name="waitUntilReady"> Whether waitable lifecycle states may be waited out before failing. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> The readiness decision result. </returns>
        Task<UnityEditorExecutionReadinessResult> EnsureExecutionReady (
            bool waitUntilReady,
            CancellationToken cancellationToken = default);
    }
}
