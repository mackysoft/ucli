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
    /// <summary> Provides reusable helpers for phase-pass implementations. </summary>
    internal static class OperationPhaseExecutionUtilities
    {
        /// <summary> Creates one standardized INVALID_ARGUMENT failure result. </summary>
        /// <param name="operationId"> The operation identifier. </param>
        /// <param name="message"> The failure message. </param>
        /// <returns> The failed phase-step result. </returns>
        public static OperationPhaseStepResult CreateInvalidArgumentFailure (
            string operationId,
            string message)
        {
            return OperationPhaseStepResult.Failed(new OperationFailure(
                Code: UcliCoreErrorCodes.InvalidArgument,
                Message: message,
                OpId: operationId));
        }

        /// <summary> Executes one phase step with exception-to-failure translation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="phase"> The phase being executed. </param>
        /// <param name="executor"> The step executor delegate. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public static async Task<OperationPhaseStepResult> ExecutePhaseStepAsync (
            NormalizedOperation operation,
            OperationPhase phase,
            Func<CancellationToken, Task<OperationPhaseStepResult>> executor,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stepResult = await executor(cancellationToken).ConfigureAwait(false);
                if (stepResult == null)
                {
                    return OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: UcliCoreErrorCodes.InternalError,
                        Message: $"Operation '{operation.Id}' returned null result at phase '{phase}'.",
                        OpId: operation.Id));
                }

                return stepResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: UcliCoreErrorCodes.InternalError,
                    Message: $"Unexpected error occurred in operation '{operation.Id}' at phase '{phase}'. {exception.Message}",
                    OpId: operation.Id));
            }
        }

        /// <summary> Merges touched entries into one target list. </summary>
        /// <param name="target"> The target touched-entry list. </param>
        /// <param name="source"> The source touched-entry collection. </param>
        public static void MergeTouched (
            List<OperationTouch> target,
            IReadOnlyList<OperationTouch> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        /// <summary> Merges diagnostic entries into one target list. </summary>
        /// <param name="target"> The target diagnostic-entry list. </param>
        /// <param name="source"> The source diagnostic-entry collection. </param>
        public static void MergeDiagnostics (
            List<OperationDiagnostic> target,
            IReadOnlyList<OperationDiagnostic> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        /// <summary> Applies operation-level response reporting policy to one phase-step result. </summary>
        /// <param name="operation"> The normalized operation that produced the result. </param>
        /// <param name="result"> The raw phase-step result. </param>
        /// <returns> The public-reporting result. </returns>
        public static OperationPhaseStepResult ApplyPersistenceReportingPolicy (
            NormalizedOperation operation,
            OperationPhaseStepResult result)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return operation.SuppressPersistenceReporting
                ? result with
                {
                    Touched = Array.Empty<OperationTouch>(),
                    ReadInvalidations = Array.Empty<OperationReadInvalidation>(),
                    Persisted = false,
                }
                : result;
        }

        /// <summary> Creates a skipped trace for operations after fail-fast stopping. </summary>
        /// <param name="operation"> The skipped operation. </param>
        /// <returns> The skipped trace entry. </returns>
        public static OperationPhaseTrace CreateSkippedTrace (NormalizedOperation operation)
        {
            return new OperationPhaseTrace(
                OpId: operation.Id,
                Op: operation.Op,
                Phase: OperationPhase.Skipped,
                Applied: false,
                Changed: false,
                Touched: Array.Empty<OperationTouch>(),
                Failure: null);
        }
    }
}
