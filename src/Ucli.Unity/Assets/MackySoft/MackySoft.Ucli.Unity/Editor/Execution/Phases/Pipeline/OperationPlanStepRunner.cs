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

            var contractFacts = OperationPhaseTrace.ContractFacts.FromMetadata(phaseOperation.Metadata);
            var preflightFailure = operationPreflight?.Invoke(operation, phaseOperation);
            if (preflightFailure != null)
            {
                return new OperationPlanStepOutcome(
                    OperationTrace: new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: false,
                        Changed: false,
                        Touched: Array.Empty<OperationTouch>(),
                        Failure: preflightFailure)
                    {
                        Result = null,
                        Contracts = contractFacts,
                    },
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
                        Diagnostics = diagnostics.ToArray(),
                        Persisted = persisted,
                        Contracts = contractFacts,
                    },
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
                        Diagnostics = diagnostics.ToArray(),
                        Persisted = persisted,
                        Contracts = contractFacts,
                    },
                    Error: planStepResult.Failure,
                    PreparedOperation: null);
            }

            var successfulTouched = touched.ToArray();
            var successfulDiagnostics = diagnostics.ToArray();
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
                    Diagnostics = successfulDiagnostics,
                    Persisted = persisted,
                    Contracts = contractFacts,
                },
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
