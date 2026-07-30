using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Accumulates validate/plan pass traces, errors, and prepared operations. </summary>
    internal sealed class PlanPassAccumulator
    {
        private readonly List<NormalizedRequestStep> compiledSteps;

        private readonly List<NormalizedOperation> compiledOperations;

        private readonly List<OperationPhaseTrace> operationTraces;

        private readonly List<OperationFailure> errors;

        private readonly List<PreparedOperation> preparedOperations;

        /// <summary> Initializes a new instance of the <see cref="PlanPassAccumulator" /> class. </summary>
        /// <param name="capacity"> The expected operation count for list preallocation. </param>
        public PlanPassAccumulator (int capacity)
        {
            compiledSteps = new List<NormalizedRequestStep>(capacity);
            compiledOperations = new List<NormalizedOperation>(capacity);
            operationTraces = new List<OperationPhaseTrace>(capacity);
            errors = new List<OperationFailure>(1);
            preparedOperations = new List<PreparedOperation>(capacity);
        }

        /// <summary> Gets a value indicating whether at least one failure has been accumulated. </summary>
        public bool HasFailures => errors.Count > 0;

        /// <summary> Adds one compiled public step and its primitive operations. </summary>
        /// <param name="step"> The compiled public step. </param>
        /// <param name="operations"> The compiled primitive operations for the step. </param>
        public void AddCompiledStep (
            NormalizedRequestStep step,
            IReadOnlyList<NormalizedOperation> operations)
        {
            compiledSteps.Add(step);
            for (var i = 0; i < operations.Count; i++)
            {
                compiledOperations.Add(operations[i]);
            }
        }

        /// <summary> Adds one skipped public step after request-level fail-fast stopped further compilation. </summary>
        /// <param name="step"> Metadata for the skipped source step. </param>
        public void AddSkippedStep (NormalizedRequestStep step)
        {
            compiledSteps.Add(step);
        }

        /// <summary> Adds one runtime-compile failure for one public step. </summary>
        /// <param name="step"> Metadata for the failed source step. </param>
        /// <param name="error"> The compile failure. </param>
        /// <param name="diagnostics"> Non-fatal diagnostics emitted before the compile failure. </param>
        public void AddCompileFailure (
            NormalizedRequestStep step,
            ExecuteRequestNormalizationError error,
            IReadOnlyList<OperationDiagnostic> diagnostics)
        {
            compiledSteps.Add(step with
            {
                PrimitiveCount = 1,
                Diagnostics = diagnostics,
            });

            var failure = new OperationFailure(error.Code, error.Message, step.Id);
            operationTraces.Add(OperationPhaseTrace.Variants.ValidationFailureBeforeContractResolution(
                step.Id,
                step.OperationName,
                failure));
            errors.Add(failure);
        }

        /// <summary> Adds one operation step outcome to accumulated pass data. </summary>
        /// <param name="outcome"> The one-operation outcome to add. </param>
        public void Add (OperationPlanStepOutcome outcome)
        {
            operationTraces.Add(outcome.OperationTrace);
            if (outcome.Error != null)
            {
                errors.Add(outcome.Error);
            }

            if (outcome.PreparedOperation != null)
            {
                preparedOperations.Add(outcome.PreparedOperation);
            }
        }

        /// <summary> Adds one skipped-operation trace. </summary>
        /// <param name="operation"> The skipped operation. </param>
        public void AddSkipped (NormalizedOperation operation)
        {
            operationTraces.Add(OperationPhaseExecutionUtilities.CreateSkippedTrace(operation));
        }

        /// <summary> Builds one immutable plan-pass result from accumulated data. </summary>
        /// <returns> The built plan-pass result. </returns>
        public PlanPassResult Build ()
        {
            return new PlanPassResult(
                CompiledSteps: compiledSteps,
                CompiledDigestPayloadUtf8: CompiledExecutionDigestWriter.WriteDigestPayload(compiledSteps, compiledOperations),
                OperationTraces: operationTraces,
                Errors: errors,
                PreparedOperations: preparedOperations);
        }
    }
}
