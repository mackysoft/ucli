using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Represents one store decision used by request-id idempotency coordination flows. </summary>
    internal readonly struct ExecuteRequestIdempotencyStoreDecision
    {
        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestIdempotencyStoreDecision" /> struct. </summary>
        /// <param name="kind"> The decision kind. </param>
        /// <param name="response"> The completed response for replay decisions. </param>
        /// <param name="sharedResponseTask"> The owner response task for wait decisions. </param>
        public ExecuteRequestIdempotencyStoreDecision (
            ExecuteRequestIdempotencyStoreDecision.DecisionKind kind,
            IpcResponse? response,
            Task<IpcResponse>? sharedResponseTask)
        {
            Kind = kind;
            Response = response;
            SharedResponseTask = sharedResponseTask;
        }

        /// <summary> Defines one idempotency decision kind. </summary>
        internal enum DecisionKind
        {
            ExecuteOwner,
            ReplayCompleted,
            WaitInFlight,
            Conflict,
        }

        /// <summary> Gets the decision kind. </summary>
        public DecisionKind Kind { get; }

        /// <summary> Gets the completed response for replay decisions. </summary>
        public IpcResponse? Response { get; }

        /// <summary> Gets the owner response task for wait decisions. </summary>
        public Task<IpcResponse>? SharedResponseTask { get; }

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
