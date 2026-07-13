using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents one lifecycle gate decision for execution requests. </summary>
    internal sealed record UnityEditorExecutionReadinessResult
    {
        private UnityEditorExecutionReadinessResult (
            UnityEditorObservation observation,
            IpcError error)
        {
            Observation = observation;
            Error = error;
        }

        /// <summary> Gets a value indicating whether execution may continue immediately. </summary>
        public bool IsReady => Error == null;

        /// <summary> Gets the Unity Editor observation captured at decision time. </summary>
        public UnityEditorObservation Observation { get; }

        /// <summary> Gets the lifecycle gate error when execution is blocked; otherwise <see langword="null" />. </summary>
        public IpcError Error { get; }

        /// <summary> Creates a successful readiness result. </summary>
        /// <param name="observation"> The Unity Editor observation captured at decision time. </param>
        /// <returns> The successful readiness result. </returns>
        public static UnityEditorExecutionReadinessResult Ready (UnityEditorObservation observation)
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
            UnityEditorObservation observation,
            IpcError error)
        {
            return new UnityEditorExecutionReadinessResult(
                observation ?? throw new ArgumentNullException(nameof(observation)),
                error ?? throw new ArgumentNullException(nameof(error)));
        }
    }
}
