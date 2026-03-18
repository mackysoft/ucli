using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Default validate/plan pass executor implementation. </summary>
    internal sealed class OperationPlanPassExecutor : IOperationPlanPassExecutor
    {
        private readonly OperationPlanStepRunner stepRunner;

        /// <summary> Initializes a new instance of the <see cref="OperationPlanPassExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationRegistry" /> is <see langword="null" />. </exception>
        public OperationPlanPassExecutor (IPhaseOperationRegistry operationRegistry)
            : this(new OperationPlanStepRunner(operationRegistry))
        {
        }

        /// <summary> Initializes a new instance of the <see cref="OperationPlanPassExecutor" /> class. </summary>
        /// <param name="stepRunner"> The one-operation plan-step runner dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stepRunner" /> is <see langword="null" />. </exception>
        internal OperationPlanPassExecutor (OperationPlanStepRunner stepRunner)
        {
            this.stepRunner = stepRunner ?? throw new ArgumentNullException(nameof(stepRunner));
        }

        /// <summary> Executes validate and plan phases for all operations with fail-fast semantics. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The plan-pass result. </returns>
        public async Task<PlanPassResult> Execute (
            NormalizedExecuteRequest request,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            var accumulator = new PlanPassAccumulator(request.Ops.Count);
            var operationUseCounts = OperationPhaseExecutionUtilities.CountOperationUse(request.Ops);

            for (var i = 0; i < request.Ops.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var operation = request.Ops[i];
                if (accumulator.HasFailures)
                {
                    accumulator.AddSkipped(operation);
                    continue;
                }

                var outcome = await stepRunner.Execute(
                    operation,
                    executionContext,
                    requiresPreCallPlanReplay: operationUseCounts[operation.Op] > 1,
                    cancellationToken).ConfigureAwait(false);
                accumulator.Add(outcome);
            }

            return accumulator.Build();
        }
    }
}