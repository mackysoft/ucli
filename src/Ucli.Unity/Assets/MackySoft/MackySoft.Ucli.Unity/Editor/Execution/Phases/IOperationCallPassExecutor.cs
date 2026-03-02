using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes request-level call pass. </summary>
    internal interface IOperationCallPassExecutor
    {
        /// <summary> Executes call phase for prevalidated and preplanned operations. </summary>
        /// <param name="preparedOperations"> The prepared operations. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The call-pass result. </returns>
        Task<CallPassResult> Execute (
            IReadOnlyList<PreparedOperation> preparedOperations,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default);
    }
}
