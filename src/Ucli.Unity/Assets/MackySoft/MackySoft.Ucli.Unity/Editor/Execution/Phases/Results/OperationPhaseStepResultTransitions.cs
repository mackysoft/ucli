using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Applies execution-pipeline transitions to operation phase-step results. </summary>
    internal static class OperationPhaseStepResultTransitions
    {
        public static OperationPhaseStepResult WithPersistenceReport (
            this OperationPhaseStepResult result,
            IReadOnlyList<OperationTouch> touched,
            IReadOnlyList<OperationReadInvalidation> readInvalidations,
            bool persisted)
        {
            return (result ?? throw new ArgumentNullException(nameof(result))) with
            {
                Touched = touched ?? throw new ArgumentNullException(nameof(touched)),
                ReadInvalidations = readInvalidations
                    ?? throw new ArgumentNullException(nameof(readInvalidations)),
                Persisted = persisted,
            };
        }

        public static OperationPhaseStepResult WithTraceAggregation (
            this OperationPhaseStepResult result,
            IReadOnlyList<OperationTouch> touched,
            IReadOnlyList<OperationDiagnostic> diagnostics,
            bool persisted)
        {
            return (result ?? throw new ArgumentNullException(nameof(result))) with
            {
                Touched = touched ?? throw new ArgumentNullException(nameof(touched)),
                Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)),
                Persisted = persisted,
            };
        }

        public static OperationPhaseStepResult ReplaceFailure (
            this OperationPhaseStepResult result,
            OperationFailure failure)
        {
            var failedResult = RequireFailedResult(result);

            return OperationPhaseStepResult.Failed(
                failure,
                failedResult.Applied,
                failedResult.Changed,
                failedResult.Result,
                failedResult.Touched) with
            {
                ReadInvalidations = failedResult.ReadInvalidations,
                Diagnostics = failedResult.Diagnostics,
                Persisted = failedResult.Persisted,
            };
        }

        private static OperationPhaseStepResult RequireFailedResult (
            OperationPhaseStepResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (result.Failure == null)
            {
                throw new InvalidOperationException("Only a failed step result can replace its failure.");
            }

            return result;
        }
    }
}
