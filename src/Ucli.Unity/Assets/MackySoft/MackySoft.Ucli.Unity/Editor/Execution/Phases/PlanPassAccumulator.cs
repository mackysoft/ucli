using System.Collections.Generic;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Accumulates validate/plan pass traces, errors, and prepared operations. </summary>
    internal sealed class PlanPassAccumulator
    {
        private readonly List<OperationPhaseTrace> operationTraces;

        private readonly List<OperationFailure> errors;

        private readonly List<PreparedOperation> preparedOperations;

        /// <summary> Initializes a new instance of the <see cref="PlanPassAccumulator" /> class. </summary>
        /// <param name="capacity"> The expected operation count for list preallocation. </param>
        public PlanPassAccumulator (int capacity)
        {
            operationTraces = new List<OperationPhaseTrace>(capacity);
            errors = new List<OperationFailure>(1);
            preparedOperations = new List<PreparedOperation>(capacity);
        }

        /// <summary> Gets a value indicating whether at least one failure has been accumulated. </summary>
        public bool HasFailures => errors.Count > 0;

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
                OperationTraces: operationTraces,
                Errors: errors,
                PreparedOperations: preparedOperations);
        }
    }
}