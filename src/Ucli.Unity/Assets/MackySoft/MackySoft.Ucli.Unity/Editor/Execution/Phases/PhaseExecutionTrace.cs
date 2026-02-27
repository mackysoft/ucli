using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one request-level execution trace produced by phase execution. </summary>
    /// <param name="ProtocolVersion"> The protocol version associated with the request. </param>
    /// <param name="RequestId"> The request identifier associated with the request. </param>
    /// <param name="OperationTraces"> The per-operation trace entries. </param>
    /// <param name="Errors"> The request-level error list. </param>
    internal sealed record PhaseExecutionTrace (
        int ProtocolVersion,
        string RequestId,
        IReadOnlyList<OperationPhaseTrace> OperationTraces,
        IReadOnlyList<OperationFailure> Errors)
    {
        /// <summary> Gets a value indicating whether execution completed without errors. </summary>
        public bool IsSuccess => Errors.Count == 0;

        /// <summary> Creates a successful request-level execution trace. </summary>
        /// <param name="protocolVersion"> The protocol version associated with the request. </param>
        /// <param name="requestId"> The request identifier associated with the request. </param>
        /// <param name="operationTraces"> The per-operation trace entries. </param>
        /// <returns> The successful execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" /> or <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        public static PhaseExecutionTrace Success (
            int protocolVersion,
            string requestId,
            IReadOnlyList<OperationPhaseTrace> operationTraces)
        {
            if (requestId == null)
            {
                throw new ArgumentNullException(nameof(requestId));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            return new PhaseExecutionTrace(
                ProtocolVersion: protocolVersion,
                RequestId: requestId,
                OperationTraces: operationTraces,
                Errors: Array.Empty<OperationFailure>());
        }

        /// <summary> Creates a failed request-level execution trace. </summary>
        /// <param name="protocolVersion"> The protocol version associated with the request. </param>
        /// <param name="requestId"> The request identifier associated with the request. </param>
        /// <param name="operationTraces"> The per-operation trace entries. </param>
        /// <param name="errors"> The request-level errors. </param>
        /// <returns> The failed execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any reference argument is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="errors" /> is empty. </exception>
        public static PhaseExecutionTrace Failure (
            int protocolVersion,
            string requestId,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            IReadOnlyList<OperationFailure> errors)
        {
            if (requestId == null)
            {
                throw new ArgumentNullException(nameof(requestId));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (errors.Count == 0)
            {
                throw new ArgumentException("Errors must not be empty.", nameof(errors));
            }

            return new PhaseExecutionTrace(
                ProtocolVersion: protocolVersion,
                RequestId: requestId,
                OperationTraces: operationTraces,
                Errors: errors);
        }
    }
}
