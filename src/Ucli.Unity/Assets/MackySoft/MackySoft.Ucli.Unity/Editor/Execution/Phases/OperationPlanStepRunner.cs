using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes validate/plan steps for one operation. </summary>
    internal sealed class OperationPlanStepRunner
    {
        private readonly IPhaseOperationRegistry operationRegistry;

        /// <summary> Initializes a new instance of the <see cref="OperationPlanStepRunner" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationRegistry" /> is <see langword="null" />. </exception>
        public OperationPlanStepRunner (IPhaseOperationRegistry operationRegistry)
        {
            this.operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
        }

        /// <summary> Executes validate and plan steps for one operation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The one-operation step outcome. </returns>
        public async Task<OperationPlanStepOutcome> Execute (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!operationRegistry.TryResolve(operation.Op, out var phaseOperation))
            {
                var missingOperationFailure = new OperationFailure(
                    Code: IpcErrorCodes.CommandNotImplemented,
                    Message: $"Operation '{operation.Op}' is not implemented.",
                    OpId: operation.Id);
                return new OperationPlanStepOutcome(
                    OperationTrace: new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: false,
                        Changed: false,
                        Touched: Array.Empty<OperationTouch>(),
                        Failure: missingOperationFailure)
                    {
                        Result = null,
                    },
                    Error: missingOperationFailure,
                    PreparedOperation: null);
            }

            var touched = new List<OperationTouch>();
            var validateStepResult = await OperationPhaseExecutionUtilities.ExecutePhaseStep(
                operation,
                OperationPhase.Validate,
                ct => phaseOperation.Validate(operation, executionContext, ct),
                cancellationToken).ConfigureAwait(false);
            OperationPhaseExecutionUtilities.MergeTouched(touched, validateStepResult.Touched);
            if (!validateStepResult.IsSuccess)
            {
                return new OperationPlanStepOutcome(
                    OperationTrace: new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: validateStepResult.Applied,
                        Changed: validateStepResult.Changed,
                        Touched: touched.ToArray(),
                        Failure: validateStepResult.Failure)
                    {
                        Result = validateStepResult.Result,
                    },
                    Error: validateStepResult.Failure,
                    PreparedOperation: null);
            }

            var planStepResult = await OperationPhaseExecutionUtilities.ExecutePhaseStep(
                operation,
                OperationPhase.Plan,
                ct => phaseOperation.Plan(operation, executionContext, ct),
                cancellationToken).ConfigureAwait(false);
            OperationPhaseExecutionUtilities.MergeTouched(touched, planStepResult.Touched);
            if (!planStepResult.IsSuccess)
            {
                return new OperationPlanStepOutcome(
                    OperationTrace: new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Plan,
                        Applied: planStepResult.Applied,
                        Changed: planStepResult.Changed,
                        Touched: touched.ToArray(),
                        Failure: planStepResult.Failure)
                    {
                        Result = planStepResult.Result,
                    },
                    Error: planStepResult.Failure,
                    PreparedOperation: null);
            }

            var successfulTouched = touched.ToArray();
            return new OperationPlanStepOutcome(
                OperationTrace: new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Plan,
                    Applied: planStepResult.Applied,
                    Changed: planStepResult.Changed,
                    Touched: successfulTouched,
                    Failure: null)
                {
                    Result = planStepResult.Result,
                },
                Error: null,
                PreparedOperation: new PreparedOperation(
                    Operation: operation,
                    PhaseOperation: phaseOperation,
                    PlanTouched: successfulTouched,
                    RequiresPreCallPlanReplay: phaseOperation.Metadata.RequiresPreCallPlanReplay));
        }
    }
}
