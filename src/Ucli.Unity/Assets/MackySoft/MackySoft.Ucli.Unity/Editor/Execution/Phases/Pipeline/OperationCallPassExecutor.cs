using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

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
        public async Task<CallPassResult> ExecuteAsync (
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
                var contractFacts = OperationContractFacts.FromMetadata(preparedOperation.PhaseOperation.Metadata);
                if (hasFailed)
                {
                    operationTraces.Add(OperationPhaseTrace.Variants.SkippedAgainstContract(
                        preparedOperation.Operation.Id,
                        preparedOperation.Operation.Op,
                        contractFacts));
                    continue;
                }

                var touched = new List<OperationTouch>(preparedOperation.PlanTouched.Count);
                var diagnostics = new List<OperationDiagnostic>();
                var persisted = preparedOperation.PlanPersisted;
                OperationPhaseExecutionUtilities.MergeTouched(touched, preparedOperation.PlanTouched);

                if (preparedOperation.RequiresPreCallPlanReplay)
                {
                    // NOTE:
                    // Some operations keep request-local plan state inside the phase-operation instance.
                    // Those operations opt into plan replay explicitly through metadata so Call observes
                    // state derived from the current operation immediately beforehand.
                    var replayedPlanStepResult = OperationPhaseExecutionUtilities.ApplyPersistenceReportingPolicy(
                        preparedOperation.Operation,
                        await OperationPhaseExecutionUtilities.ExecutePhaseStepAsync(
                            preparedOperation.Operation,
                            OperationPhase.Plan,
                            ct => preparedOperation.PhaseOperation.PlanAsync(preparedOperation.Operation, executionContext, ct),
                            cancellationToken));
                    OperationPhaseExecutionUtilities.MergeTouched(touched, replayedPlanStepResult.Touched);
                    OperationPhaseExecutionUtilities.MergeDiagnostics(diagnostics, replayedPlanStepResult.Diagnostics);
                    persisted |= replayedPlanStepResult.Persisted;

                    if (!replayedPlanStepResult.IsSuccess)
                    {
                        var replayTouchedSnapshot = touched.ToArray();
                        operationTraces.Add(OperationPhaseTrace.Variants.PlanFailure(
                            opId: preparedOperation.Operation.Id,
                            op: preparedOperation.Operation.Op,
                            outcome: replayedPlanStepResult.WithTraceAggregation(
                                replayTouchedSnapshot,
                                diagnostics.ToArray(),
                                persisted),
                            contracts: contractFacts));
                        errors.Add(replayedPlanStepResult.Failure!);
                        hasFailed = true;
                        continue;
                    }
                }

                var callStepResult = OperationPhaseExecutionUtilities.ApplyPersistenceReportingPolicy(
                    preparedOperation.Operation,
                    await OperationPhaseExecutionUtilities.ExecutePhaseStepAsync(
                        preparedOperation.Operation,
                        OperationPhase.Call,
                        ct => preparedOperation.PhaseOperation.CallAsync(preparedOperation.Operation, executionContext, ct),
                        cancellationToken));

                OperationPhaseExecutionUtilities.MergeTouched(touched, callStepResult.Touched);
                OperationPhaseExecutionUtilities.MergeDiagnostics(diagnostics, callStepResult.Diagnostics);
                persisted |= callStepResult.Persisted;
                var touchedSnapshot = touched.ToArray();
                var diagnosticsSnapshot = diagnostics.ToArray();

                if (!callStepResult.IsSuccess)
                {
                    operationTraces.Add(OperationPhaseTrace.Variants.CallFailure(
                        opId: preparedOperation.Operation.Id,
                        op: preparedOperation.Operation.Op,
                        outcome: callStepResult.WithTraceAggregation(
                            touchedSnapshot,
                            diagnosticsSnapshot,
                            persisted),
                        contracts: contractFacts));
                    errors.Add(callStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                operationTraces.Add(callStepResult.Verdict.HasValue
                    ? OperationPhaseTrace.Variants.CallSuccessWithVerdict(
                        opId: preparedOperation.Operation.Id,
                        op: preparedOperation.Operation.Op,
                        outcome: callStepResult.WithTraceAggregation(
                            touchedSnapshot,
                            diagnosticsSnapshot,
                            persisted),
                        contracts: contractFacts)
                    : OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        opId: preparedOperation.Operation.Id,
                        op: preparedOperation.Operation.Op,
                        outcome: callStepResult.WithTraceAggregation(
                            touchedSnapshot,
                            diagnosticsSnapshot,
                            persisted),
                        contracts: contractFacts));
            }

            return new CallPassResult(
                OperationTraces: operationTraces,
                Errors: errors);
        }
    }
}
