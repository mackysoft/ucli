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

        private readonly ExecuteRequestCompiler requestCompiler;

        /// <summary> Initializes a new instance of the <see cref="OperationPlanPassExecutor" /> class. </summary>
        /// <param name="stepRunner"> The one-operation plan-step runner dependency. </param>
        /// <param name="requestCompiler"> The runtime request compiler dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stepRunner" /> is <see langword="null" />. </exception>
        public OperationPlanPassExecutor (
            OperationPlanStepRunner stepRunner,
            ExecuteRequestCompiler requestCompiler)
        {
            this.stepRunner = stepRunner ?? throw new ArgumentNullException(nameof(stepRunner));
            this.requestCompiler = requestCompiler ?? throw new ArgumentNullException(nameof(requestCompiler));
        }

        /// <summary> Executes validate and plan phases for all operations with fail-fast semantics. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="operationPreflight"> Optional preflight executed after operation resolution and before validate/plan execution. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The plan-pass result. </returns>
        public async Task<PlanPassResult> ExecuteAsync (
            NormalizedExecuteRequest request,
            OperationExecutionContext executionContext,
            Func<NormalizedOperation, IUcliOperation, OperationFailure?>? operationPreflight,
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

            var accumulator = new PlanPassAccumulator(request.SourceSteps.Count);
            for (var stepIndex = 0; stepIndex < request.SourceSteps.Count; stepIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (accumulator.HasFailures)
                {
                    accumulator.AddSkippedStep(request.SourceSteps[stepIndex]);
                    continue;
                }

                var sourceStep = request.SourceSteps[stepIndex];
                if (!requestCompiler.TryCompileExecutionStep(
                        sourceStep,
                        executionContext,
                        out var compiledStep,
                        out var compiledOperations,
                        out var compileDiagnostics,
                        out var compileError))
                {
                    accumulator.AddCompileFailure(sourceStep, compileError, compileDiagnostics);
                    continue;
                }

                accumulator.AddCompiledStep(compiledStep, compiledOperations);
                for (var operationIndex = 0; operationIndex < compiledOperations.Count; operationIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var operation = compiledOperations[operationIndex];
                    if (accumulator.HasFailures)
                    {
                        accumulator.AddSkipped(operation);
                        continue;
                    }

                    var outcome = await stepRunner.ExecuteAsync(
                        operation,
                        executionContext,
                        operationPreflight,
                        cancellationToken).ConfigureAwait(false);
                    accumulator.Add(outcome);
                }
            }

            return accumulator.Build();
        }
    }
}
