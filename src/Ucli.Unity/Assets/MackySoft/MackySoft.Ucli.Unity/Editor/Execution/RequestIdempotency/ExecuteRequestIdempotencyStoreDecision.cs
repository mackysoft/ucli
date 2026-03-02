using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Represents one store decision used by request-id idempotency coordination flows. </summary>
    /// <param name="Kind"> The decision kind. </param>
    /// <param name="Response"> The completed response for replay decisions. </param>
    /// <param name="SharedResponseTask"> The owner response task for wait decisions. </param>
    internal readonly record struct ExecuteRequestIdempotencyStoreDecision (
        ExecuteRequestIdempotencyStoreDecision.DecisionKind Kind,
        IpcResponse? Response,
        Task<IpcResponse>? SharedResponseTask)
    {
        /// <summary> Defines one idempotency decision kind. </summary>
        internal enum DecisionKind
        {
            ExecuteOwner,
            ReplayCompleted,
            WaitInFlight,
            Conflict,
        }

        /// <summary> Creates one owner-execution decision. </summary>
        /// <returns> The owner-execution decision. </returns>
        public static ExecuteRequestIdempotencyStoreDecision ExecuteOwner ()
        {
            return new ExecuteRequestIdempotencyStoreDecision(DecisionKind.ExecuteOwner, null, null);
        }

        /// <summary> Creates one replay-completed decision. </summary>
        /// <param name="response"> The completed response to replay. </param>
        /// <returns> The replay decision. </returns>
        public static ExecuteRequestIdempotencyStoreDecision ReplayCompleted (IpcResponse response)
        {
            return new ExecuteRequestIdempotencyStoreDecision(DecisionKind.ReplayCompleted, response, null);
        }

        /// <summary> Creates one wait-in-flight decision. </summary>
        /// <param name="sharedResponseTask"> The shared owner-response task. </param>
        /// <returns> The wait decision. </returns>
        public static ExecuteRequestIdempotencyStoreDecision WaitInFlight (Task<IpcResponse> sharedResponseTask)
        {
            return new ExecuteRequestIdempotencyStoreDecision(DecisionKind.WaitInFlight, null, sharedResponseTask);
        }

        /// <summary> Creates one conflict decision. </summary>
        /// <returns> The conflict decision. </returns>
        public static ExecuteRequestIdempotencyStoreDecision Conflict ()
        {
            return new ExecuteRequestIdempotencyStoreDecision(DecisionKind.Conflict, null, null);
        }
    }
}
