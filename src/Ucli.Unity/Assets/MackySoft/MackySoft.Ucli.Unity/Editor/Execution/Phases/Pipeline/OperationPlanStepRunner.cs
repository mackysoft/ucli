using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
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
        /// <param name="operationPreflight"> Optional preflight executed after operation resolution and before validate/plan execution. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The one-operation step outcome. </returns>
        public async Task<OperationPlanStepOutcome> ExecuteAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            Func<NormalizedOperation, IUcliOperation, OperationFailure?>? operationPreflight,
            CancellationToken cancellationToken = default)
        {
            if (!operationRegistry.TryResolve(operation.Op, out var phaseOperation))
            {
                var missingOperationFailure = new OperationFailure(
                    Code: UcliCoreErrorCodes.CommandNotImplemented,
                    Message: $"Operation '{operation.Op}' is not implemented.",
                    OpId: operation.Id);
                return new OperationPlanStepOutcome(
                    OperationTrace: OperationPhaseTrace.Variants.ValidationFailureBeforeContractResolution(
                        operation.Id,
                        operation.Op,
                        missingOperationFailure),
                    Error: missingOperationFailure,
                    PreparedOperation: null);
            }

            var contractFacts = OperationContractFacts.FromMetadata(phaseOperation.Metadata);
            var preflightFailure = operationPreflight?.Invoke(operation, phaseOperation);
            if (preflightFailure != null)
            {
                return new OperationPlanStepOutcome(
                    OperationTrace: OperationPhaseTrace.Variants.ValidationFailure(
                        opId: operation.Id,
                        op: operation.Op,
                        outcome: OperationPhaseStepResult.Failed(
                            preflightFailure,
                            applied: false,
                            changed: false,
                            result: null,
                            Array.Empty<OperationTouch>()),
                        contracts: contractFacts),
                    Error: preflightFailure,
                    PreparedOperation: null);
            }

            var touched = new List<OperationTouch>();
            var diagnostics = new List<OperationDiagnostic>();
            var persisted = false;
            var validateStepResult = OperationPhaseExecutionUtilities.ApplyPersistenceReportingPolicy(
                operation,
                await OperationPhaseExecutionUtilities.ExecutePhaseStepAsync(
                    operation,
                    OperationPhase.Validate,
                    ct => phaseOperation.ValidateAsync(operation, executionContext, ct),
                    cancellationToken));
            OperationPhaseExecutionUtilities.MergeTouched(touched, validateStepResult.Touched);
            OperationPhaseExecutionUtilities.MergeDiagnostics(diagnostics, validateStepResult.Diagnostics);
            persisted |= validateStepResult.Persisted;
            if (!validateStepResult.IsSuccess)
            {
                return new OperationPlanStepOutcome(
                    OperationTrace: OperationPhaseTrace.Variants.ValidationFailure(
                        opId: operation.Id,
                        op: operation.Op,
                        outcome: validateStepResult.WithTraceAggregation(
                            touched.ToArray(),
                            diagnostics.ToArray(),
                            persisted),
                        contracts: contractFacts),
                    Error: validateStepResult.Failure,
                    PreparedOperation: null);
            }

            var planStepResult = OperationPhaseExecutionUtilities.ApplyPersistenceReportingPolicy(
                operation,
                await OperationPhaseExecutionUtilities.ExecutePhaseStepAsync(
                    operation,
                    OperationPhase.Plan,
                    ct => phaseOperation.PlanAsync(operation, executionContext, ct),
                    cancellationToken));
            OperationPhaseExecutionUtilities.MergeTouched(touched, planStepResult.Touched);
            OperationPhaseExecutionUtilities.MergeDiagnostics(diagnostics, planStepResult.Diagnostics);
            persisted |= planStepResult.Persisted;
            if (!planStepResult.IsSuccess)
            {
                return new OperationPlanStepOutcome(
                    OperationTrace: OperationPhaseTrace.Variants.PlanFailure(
                        opId: operation.Id,
                        op: operation.Op,
                        outcome: planStepResult.WithTraceAggregation(
                            touched.ToArray(),
                            diagnostics.ToArray(),
                            persisted),
                        contracts: contractFacts),
                    Error: planStepResult.Failure,
                    PreparedOperation: null);
            }

            var successfulTouched = touched.ToArray();
            var successfulDiagnostics = diagnostics.ToArray();
            return new OperationPlanStepOutcome(
                OperationTrace: OperationPhaseTrace.Variants.PlanSuccess(
                    opId: operation.Id,
                    op: operation.Op,
                    outcome: planStepResult.WithTraceAggregation(
                        successfulTouched,
                        successfulDiagnostics,
                        persisted),
                    contracts: contractFacts),
                Error: null,
                PreparedOperation: new PreparedOperation(
                    Operation: operation,
                    PhaseOperation: phaseOperation,
                    PlanTouched: successfulTouched,
                    PlanPersisted: persisted,
                    RequiresPreCallPlanReplay: phaseOperation.Metadata.RequiresPreCallPlanReplay));
        }
    }
}
