using System;
using System.Collections.Generic;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one request-level execution trace produced by phase execution. </summary>
    internal sealed class PhaseExecutionTrace
    {
        private PhaseExecutionTrace (
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            string? planToken,
            IReadOnlyList<OperationFailure> errors)
        {
            Steps = Copy(steps);
            OperationTraces = Copy(operationTraces);
            PlanToken = planToken;
            Errors = Copy(errors);
        }

        /// <summary> Gets the normalized public step list used for response aggregation. </summary>
        public IReadOnlyList<NormalizedRequestStep> Steps { get; }

        /// <summary> Gets the per-operation trace entries. </summary>
        public IReadOnlyList<OperationPhaseTrace> OperationTraces { get; }

        /// <summary> Gets the optional plan token issued for successful plan execution. </summary>
        public string? PlanToken { get; }

        /// <summary> Gets the request-level errors. </summary>
        public IReadOnlyList<OperationFailure> Errors { get; }

        /// <summary> Gets a value indicating whether execution completed without errors. </summary>
        public bool IsSuccess => Errors.Count == 0;

        /// <summary> Creates a successful request-level execution trace for a command that does not issue a plan token. </summary>
        /// <param name="steps"> The normalized public step list used for response aggregation. </param>
        /// <param name="operationTraces"> The per-operation trace entries. </param>
        /// <returns> The successful execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="steps" /> or <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        public static PhaseExecutionTrace SucceededWithoutPlanToken (
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<OperationPhaseTrace> operationTraces)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            return new PhaseExecutionTrace(
                steps,
                operationTraces,
                planToken: null,
                Array.Empty<OperationFailure>());
        }

        /// <summary> Creates a successful plan trace with its issued token. </summary>
        /// <param name="steps"> The normalized public step list used for response aggregation. </param>
        /// <param name="operationTraces"> The per-operation trace entries. </param>
        /// <param name="planToken"> The non-empty token issued for the successful plan. </param>
        /// <returns> The successful plan trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="steps" /> or <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="planToken" /> is empty or whitespace. </exception>
        public static PhaseExecutionTrace PlanSucceeded (
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            string planToken)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            if (string.IsNullOrWhiteSpace(planToken))
            {
                throw new ArgumentException("Plan token must not be empty.", nameof(planToken));
            }

            return new PhaseExecutionTrace(
                steps,
                operationTraces,
                planToken,
                Array.Empty<OperationFailure>());
        }

        /// <summary> Creates a failed request-level execution trace. </summary>
        /// <param name="steps"> The normalized public step list used for response aggregation. </param>
        /// <param name="operationTraces"> The per-operation trace entries. </param>
        /// <param name="errors"> The request-level errors. </param>
        /// <returns> The failed execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any reference argument is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="errors" /> is empty. </exception>
        public static PhaseExecutionTrace Failed (
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            IReadOnlyList<OperationFailure> errors)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (errors.Count == 0)
            {
                throw new ArgumentException("Errors must not be empty.", nameof(errors));
            }

            return new PhaseExecutionTrace(
                steps,
                operationTraces,
                planToken: null,
                errors);
        }

        private static IReadOnlyList<T> Copy<T> (IReadOnlyList<T> source)
        {
            var snapshot = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                snapshot[i] = source[i];
            }

            return Array.AsReadOnly(snapshot);
        }
    }
}
