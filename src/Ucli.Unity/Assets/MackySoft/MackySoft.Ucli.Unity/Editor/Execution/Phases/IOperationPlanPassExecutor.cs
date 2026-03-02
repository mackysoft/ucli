using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes request-level validate/plan pass. </summary>
    internal interface IOperationPlanPassExecutor
    {
        /// <summary> Executes validate/plan phases for all operations with fail-fast semantics. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The plan-pass result. </returns>
        Task<PlanPassResult> Execute (
            NormalizedExecuteRequest request,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default);
    }
}
