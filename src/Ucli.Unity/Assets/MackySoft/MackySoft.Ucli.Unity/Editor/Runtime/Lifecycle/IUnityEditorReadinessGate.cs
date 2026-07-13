using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Captures editor lifecycle telemetry and gates execution requests. </summary>
    internal interface IUnityEditorReadinessGate
    {
        /// <summary> Captures the current Unity Editor observation. </summary>
        /// <returns> The normalized Unity Editor observation. </returns>
        UnityEditorObservation CaptureObservation ();

        /// <summary> Evaluates Editor lifecycle readiness for a request that has already entered its execution lane. </summary>
        /// <param name="failFast"> Whether waitable lifecycle states should fail immediately instead of being waited out. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <param name="allowPlayMode"> Whether a GUI Editor Play Mode session may accept mutation execution. </param>
        /// <returns> The readiness decision based on Editor lifecycle state without the execution-lane busy overlay. </returns>
        Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
            bool failFast,
            CancellationToken cancellationToken = default,
            bool allowPlayMode = false);
    }
}
