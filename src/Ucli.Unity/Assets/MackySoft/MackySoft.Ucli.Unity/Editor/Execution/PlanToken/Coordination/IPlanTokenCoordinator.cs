using System.Collections.Generic;
using System.Threading;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Coordinates plan-token issuance and validation around phase execution. </summary>
    internal interface IPlanTokenCoordinator
    {
        /// <summary> Issues one plan token from normalized request and plan traces. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The plan-phase operation traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The token issue result. </returns>
        PlanTokenIssueResult Issue (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default);

        /// <summary> Validates one incoming call plan token against request and current state. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The pre-call plan traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The validation result. </returns>
        PlanTokenValidationResult ValidateCall (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default);
    }
}