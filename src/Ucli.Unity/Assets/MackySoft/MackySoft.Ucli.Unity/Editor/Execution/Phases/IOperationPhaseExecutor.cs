using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes normalized operations through phase execution. </summary>
    internal interface IOperationPhaseExecutor
    {
        /// <summary> Executes one normalized request through the specified command phase-flow. </summary>
        /// <param name="command"> The top-level execution command. </param>
        /// <param name="request"> The normalized request. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The request-level execution trace. </returns>
        Task<PhaseExecutionTrace> Execute (
            PhaseExecutionCommand command,
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken = default);
    }
}
