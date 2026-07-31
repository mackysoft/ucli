using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents one lifecycle gate decision for execution requests. </summary>
    internal sealed record UnityEditorExecutionReadinessResult
    {
        private UnityEditorExecutionReadinessResult (
            UnityEditorRuntimeObservation observation,
            IpcError error)
        {
            Observation = observation;
            Error = error;
        }

        /// <summary> Gets a value indicating whether execution may continue immediately. </summary>
        public bool IsReady => Error == null;

        /// <summary> Gets the Unity Editor observation captured at decision time. </summary>
        public UnityEditorRuntimeObservation Observation { get; }

        /// <summary> Gets the lifecycle gate error when execution is blocked; otherwise <see langword="null" />. </summary>
        public IpcError Error { get; }

        /// <summary> Creates a successful readiness result. </summary>
        /// <param name="observation"> The Unity Editor observation captured at decision time. </param>
        /// <returns> The successful readiness result. </returns>
        public static UnityEditorExecutionReadinessResult Ready (UnityEditorRuntimeObservation observation)
        {
            return new UnityEditorExecutionReadinessResult(
                observation ?? throw new ArgumentNullException(nameof(observation)),
                error: null);
        }

        /// <summary> Creates a failed readiness result. </summary>
        /// <param name="observation"> The Unity Editor observation captured at decision time. </param>
        /// <param name="error"> The lifecycle gate error. </param>
        /// <returns> The failed readiness result. </returns>
        public static UnityEditorExecutionReadinessResult Blocked (
            UnityEditorRuntimeObservation observation,
            IpcError error)
        {
            return new UnityEditorExecutionReadinessResult(
                observation ?? throw new ArgumentNullException(nameof(observation)),
                error ?? throw new ArgumentNullException(nameof(error)));
        }
    }
}
