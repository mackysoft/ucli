using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Captures local Editor runtime state and gates execution requests. </summary>
    internal interface IUnityEditorReadinessGate
    {
        /// <summary> Captures the underlying Unity Editor observation without host execution-lane availability. </summary>
        /// <returns> The local runtime snapshot used by an operation that already owns its execution lane. </returns>
        UnityEditorRuntimeObservation CaptureObservation ();

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

    /// <summary> Captures local host availability from Editor state and mutation-lane ownership. </summary>
    internal interface IUnityEditorAvailabilityObservationSource
    {
        /// <summary> Captures the current Unity Editor observation including mutation-lane availability. </summary>
        /// <returns> The local runtime snapshot projected by status and lifecycle sidecar boundaries. </returns>
        UnityEditorRuntimeObservation CaptureAvailabilityObservation ();
    }
}
