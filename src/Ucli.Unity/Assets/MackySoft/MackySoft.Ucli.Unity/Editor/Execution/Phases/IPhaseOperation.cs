using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Defines one operation implementation used by phase execution. </summary>
    internal interface IPhaseOperation
    {
        /// <summary> Gets the operation name served by this implementation. </summary>
        string OperationName { get; }

        /// <summary> Executes the validate phase for one operation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            CancellationToken cancellationToken = default);

        /// <summary> Executes the plan phase for one operation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            CancellationToken cancellationToken = default);

        /// <summary> Executes the call phase for one operation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            CancellationToken cancellationToken = default);
    }
}
