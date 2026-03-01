using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Default validate/plan pass executor implementation. </summary>
    internal sealed class OperationPlanPassExecutor : IOperationPlanPassExecutor
    {
        private readonly IPhaseOperationRegistry operationRegistry;

        /// <summary> Initializes a new instance of the <see cref="OperationPlanPassExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationRegistry" /> is <see langword="null" />. </exception>
        public OperationPlanPassExecutor (IPhaseOperationRegistry operationRegistry)
        {
            this.operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
        }

        /// <summary> Executes validate and plan phases for all operations with fail-fast semantics. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The plan-pass result. </returns>
        public async Task<PlanPassResult> Execute (
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var operationTraces = new List<OperationPhaseTrace>(request.Ops.Count);
            var errors = new List<OperationFailure>(1);
            var preparedOperations = new List<PreparedOperation>(request.Ops.Count);
            var operationUseCounts = OperationPhaseExecutionUtilities.CountOperationUse(request.Ops);
            var hasFailed = false;

            for (var i = 0; i < request.Ops.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var operation = request.Ops[i];
                if (hasFailed)
                {
                    operationTraces.Add(OperationPhaseExecutionUtilities.CreateSkippedTrace(operation));
                    continue;
                }

                if (!operationRegistry.TryResolve(operation.Op, out var phaseOperation))
                {
                    var missingOperationFailure = new OperationFailure(
                        Code: IpcErrorCodes.CommandNotImplemented,
                        Message: $"Operation '{operation.Op}' is not implemented.",
                        OpId: operation.Id);
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: false,
                        Changed: false,
                        Touched: Array.Empty<OperationTouch>(),
                        Failure: missingOperationFailure));
                    errors.Add(missingOperationFailure);
                    hasFailed = true;
                    continue;
                }

                var touched = new List<OperationTouch>();
                var validateStepResult = await OperationPhaseExecutionUtilities.ExecutePhaseStep(
                    operation,
                    OperationPhase.Validate,
                    ct => phaseOperation.Validate(operation, ct),
                    cancellationToken).ConfigureAwait(false);
                OperationPhaseExecutionUtilities.MergeTouched(touched, validateStepResult.Touched);
                if (!validateStepResult.IsSuccess)
                {
                    var touchedSnapshot = touched.ToArray();
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: validateStepResult.Applied,
                        Changed: validateStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: validateStepResult.Failure));
                    errors.Add(validateStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                var planStepResult = await OperationPhaseExecutionUtilities.ExecutePhaseStep(
                    operation,
                    OperationPhase.Plan,
                    ct => phaseOperation.Plan(operation, ct),
                    cancellationToken).ConfigureAwait(false);
                OperationPhaseExecutionUtilities.MergeTouched(touched, planStepResult.Touched);
                if (!planStepResult.IsSuccess)
                {
                    var touchedSnapshot = touched.ToArray();
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Plan,
                        Applied: planStepResult.Applied,
                        Changed: planStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: planStepResult.Failure));
                    errors.Add(planStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                var successfulTouched = touched.ToArray();
                operationTraces.Add(new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Plan,
                    Applied: planStepResult.Applied,
                    Changed: planStepResult.Changed,
                    Touched: successfulTouched,
                    Failure: null));
                preparedOperations.Add(new PreparedOperation(
                    Operation: operation,
                    PhaseOperation: phaseOperation,
                    PlanTouched: successfulTouched,
                    RequiresPreCallPlanReplay: operationUseCounts[operation.Op] > 1));
            }

            return new PlanPassResult(
                OperationTraces: operationTraces,
                Errors: errors,
                PreparedOperations: preparedOperations);
        }
    }
}
