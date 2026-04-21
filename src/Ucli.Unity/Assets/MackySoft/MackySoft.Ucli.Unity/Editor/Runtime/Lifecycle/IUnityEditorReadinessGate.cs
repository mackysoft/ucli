using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Captures editor lifecycle telemetry and gates execution requests. </summary>
    internal interface IUnityEditorReadinessGate
    {
        /// <summary> Captures the current editor lifecycle snapshot. </summary>
        /// <returns> The normalized lifecycle snapshot. </returns>
        UnityEditorLifecycleSnapshot CaptureSnapshot ();

        /// <summary> Ensures the editor can accept execution requests, optionally failing fast. </summary>
        /// <param name="failFast"> Whether waitable lifecycle states should fail immediately instead of being waited out. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> The readiness decision result. </returns>
        Task<UnityEditorExecutionReadinessResult> EnsureExecutionReady (
            bool failFast,
            CancellationToken cancellationToken = default);
    }
}
