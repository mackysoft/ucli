using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Default call-pass executor implementation. </summary>
    internal sealed class OperationCallPassExecutor : IOperationCallPassExecutor
    {
        /// <summary> Executes call phase for prevalidated and preplanned operations. </summary>
        /// <param name="preparedOperations"> The prepared operations. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The call-pass result. </returns>
        public async Task<CallPassResult> Execute (
            IReadOnlyList<PreparedOperation> preparedOperations,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (preparedOperations == null)
            {
                throw new ArgumentNullException(nameof(preparedOperations));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            var operationTraces = new List<OperationPhaseTrace>(preparedOperations.Count);
            var errors = new List<OperationFailure>(1);
            var hasFailed = false;

            for (var i = 0; i < preparedOperations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var preparedOperation = preparedOperations[i];
                if (hasFailed)
                {
                    operationTraces.Add(OperationPhaseExecutionUtilities.CreateSkippedTrace(preparedOperation.Operation));
                    continue;
                }

                var touched = new List<OperationTouch>(preparedOperation.PlanTouched.Count);
                OperationPhaseExecutionUtilities.MergeTouched(touched, preparedOperation.PlanTouched);

                if (preparedOperation.RequiresPreCallPlanReplay)
                {
                    // NOTE:
                    // Some operations keep request-local plan state inside the phase-operation instance.
                    // Those operations opt into plan replay explicitly through metadata so Call observes
                    // state derived from the current operation immediately beforehand.
                    var replayedPlanStepResult = await OperationPhaseExecutionUtilities.ExecutePhaseStep(
                        preparedOperation.Operation,
                        OperationPhase.Plan,
                        ct => preparedOperation.PhaseOperation.Plan(preparedOperation.Operation, executionContext, ct),
                        cancellationToken).ConfigureAwait(false);
                    OperationPhaseExecutionUtilities.MergeTouched(touched, replayedPlanStepResult.Touched);

                    if (!replayedPlanStepResult.IsSuccess)
                    {
                        var replayTouchedSnapshot = touched.ToArray();
                        operationTraces.Add(new OperationPhaseTrace(
                            OpId: preparedOperation.Operation.Id,
                            Op: preparedOperation.Operation.Op,
                            Phase: OperationPhase.Plan,
                            Applied: replayedPlanStepResult.Applied,
                            Changed: replayedPlanStepResult.Changed,
                            Touched: replayTouchedSnapshot,
                            Failure: replayedPlanStepResult.Failure)
                        {
                            Result = replayedPlanStepResult.Result,
                        });
                        errors.Add(replayedPlanStepResult.Failure!);
                        hasFailed = true;
                        continue;
                    }
                }

                var callStepResult = await OperationPhaseExecutionUtilities.ExecutePhaseStep(
                    preparedOperation.Operation,
                    OperationPhase.Call,
                    ct => preparedOperation.PhaseOperation.Call(preparedOperation.Operation, executionContext, ct),
                    cancellationToken).ConfigureAwait(false);

                OperationPhaseExecutionUtilities.MergeTouched(touched, callStepResult.Touched);
                var touchedSnapshot = touched.ToArray();

                if (!callStepResult.IsSuccess)
                {
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: preparedOperation.Operation.Id,
                        Op: preparedOperation.Operation.Op,
                        Phase: OperationPhase.Call,
                        Applied: callStepResult.Applied,
                        Changed: callStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: callStepResult.Failure)
                    {
                        Result = callStepResult.Result,
                    });
                    errors.Add(callStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                operationTraces.Add(new OperationPhaseTrace(
                    OpId: preparedOperation.Operation.Id,
                    Op: preparedOperation.Operation.Op,
                    Phase: OperationPhase.Call,
                    Applied: callStepResult.Applied,
                    Changed: callStepResult.Changed,
                    Touched: touchedSnapshot,
                    Failure: null)
                {
                    Result = callStepResult.Result,
                });
            }

            return new CallPassResult(
                OperationTraces: operationTraces,
                Errors: errors);
        }
    }
}
