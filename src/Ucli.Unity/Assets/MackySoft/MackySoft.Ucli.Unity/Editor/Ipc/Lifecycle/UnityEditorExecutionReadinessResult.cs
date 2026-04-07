using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one lifecycle gate decision for execution requests. </summary>
    internal sealed record UnityEditorExecutionReadinessResult (
        UnityEditorLifecycleSnapshot Snapshot,
        IpcError? Error)
    {
        /// <summary> Gets a value indicating whether execution may continue immediately. </summary>
        public bool IsReady => Error == null;

        /// <summary> Creates a successful readiness result. </summary>
        /// <param name="snapshot"> The lifecycle snapshot captured at decision time. </param>
        /// <returns> The successful readiness result. </returns>
        public static UnityEditorExecutionReadinessResult Ready (UnityEditorLifecycleSnapshot snapshot)
        {
            return new UnityEditorExecutionReadinessResult(snapshot, null);
        }

        /// <summary> Creates a failed readiness result. </summary>
        /// <param name="snapshot"> The lifecycle snapshot captured at decision time. </param>
        /// <param name="error"> The lifecycle gate error. </param>
        /// <returns> The failed readiness result. </returns>
        public static UnityEditorExecutionReadinessResult Blocked (
            UnityEditorLifecycleSnapshot snapshot,
            IpcError error)
        {
            return new UnityEditorExecutionReadinessResult(snapshot, error);
        }
    }
}
