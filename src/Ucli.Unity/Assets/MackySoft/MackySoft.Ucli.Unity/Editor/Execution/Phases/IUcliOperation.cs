using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Defines one extensible operation contract executed by validate/plan/call phases. </summary>
    public interface IUcliOperation
    {
        /// <summary> Gets the operation metadata. </summary>
        UcliOperationMetadata Metadata { get; }

        /// <summary> Executes the validate phase for one operation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default);

        /// <summary> Executes the plan phase for one operation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default);

        /// <summary> Executes the call phase for one operation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default);
    }
}