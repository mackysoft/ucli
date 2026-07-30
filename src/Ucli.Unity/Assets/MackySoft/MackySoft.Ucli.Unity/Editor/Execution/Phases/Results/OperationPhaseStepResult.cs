using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one phase-step result for one operation. </summary>
    public sealed record OperationPhaseStepResult
    {
        private OperationPhaseStepResult (
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch> touched,
            OperationFailure? failure)
        {
            Applied = applied;
            Changed = changed;
            Touched = touched ?? throw new ArgumentNullException(nameof(touched));
            Failure = failure;
        }

        /// <summary> Gets a value indicating whether the operation was applied in this step. </summary>
        public bool Applied { get; }

        /// <summary> Gets a value indicating whether this step produced changes. </summary>
        public bool Changed { get; }

        /// <summary> Gets the touched persistence-unit list produced by this step. </summary>
        public IReadOnlyList<OperationTouch> Touched { get; internal init; }

        /// <summary> Gets the failure details when this step failed; otherwise <see langword="null" />. </summary>
        public OperationFailure? Failure { get; }

        /// <summary> Gets the optional query result payload produced by this step. </summary>
        public JsonElement? Result { get; internal init; }

        internal object? TypedResult { get; init; }

        /// <summary> Gets the verdict established from a typed successful Call result. </summary>
        internal Verdict? Verdict { get; init; }

        /// <summary> Gets the optional read-surface invalidations emitted by this step. </summary>
        internal IReadOnlyList<OperationReadInvalidation> ReadInvalidations { get; init; } = Array.Empty<OperationReadInvalidation>();

        /// <summary> Gets non-fatal diagnostics emitted by this step. </summary>
        public IReadOnlyList<OperationDiagnostic> Diagnostics { get; init; } = Array.Empty<OperationDiagnostic>();

        /// <summary> Gets a value indicating whether this step observed successful persistence. </summary>
        internal bool Persisted { get; init; }

        /// <summary> Gets a value indicating whether this step succeeded. </summary>
        public bool IsSuccess => Failure is null;

        internal static OperationPhaseStepResult SuccessWithTypedResult (
            object result,
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch> touched)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (touched == null)
            {
                throw new ArgumentNullException(nameof(touched));
            }

            return new OperationPhaseStepResult(
                applied,
                changed,
                touched,
                failure: null)
            {
                TypedResult = result,
            };
        }

        /// <summary> Creates a successful phase-step result. </summary>
        /// <param name="applied"> Whether operation was applied in this step. </param>
        /// <param name="changed"> Whether changes were produced in this step. </param>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The successful phase-step result. </returns>
        public static OperationPhaseStepResult Success (
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch> touched)
        {
            if (touched == null)
            {
                throw new ArgumentNullException(nameof(touched));
            }

            return new OperationPhaseStepResult(
                applied,
                changed,
                touched,
                failure: null);
        }

        /// <summary> Creates a failed phase-step result. </summary>
        /// <param name="failure"> The operation failure details. </param>
        /// <param name="applied"> Whether operation was applied in this step. </param>
        /// <param name="changed"> Whether changes were produced in this step. </param>
        /// <param name="result"> The result evidence produced before the failure; otherwise <see langword="null" />. </param>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The failed phase-step result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static OperationPhaseStepResult Failed (
            OperationFailure failure,
            bool applied,
            bool changed,
            JsonElement? result,
            IReadOnlyList<OperationTouch> touched)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }
            if (touched == null)
            {
                throw new ArgumentNullException(nameof(touched));
            }

            return new OperationPhaseStepResult(
                applied,
                changed,
                touched,
                failure)
            {
                Result = CloneResult(result),
            };
        }

        /// <summary> Returns a copy with the supplied read-surface invalidations. </summary>
        /// <param name="readInvalidations"> The invalidations to attach to the step result. </param>
        /// <returns> One copied step result carrying the supplied invalidations. </returns>
        internal OperationPhaseStepResult WithReadInvalidations (IReadOnlyList<OperationReadInvalidation> readInvalidations)
        {
            if (readInvalidations == null)
            {
                throw new ArgumentNullException(nameof(readInvalidations));
            }

            return this with
            {
                ReadInvalidations = readInvalidations,
            };
        }

        /// <summary> Returns a copy with the supplied diagnostics. </summary>
        /// <param name="diagnostics"> The diagnostics to attach to the step result. </param>
        /// <returns> One copied step result carrying the supplied diagnostics. </returns>
        public OperationPhaseStepResult WithDiagnostics (IReadOnlyList<OperationDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            return this with
            {
                Diagnostics = diagnostics,
            };
        }

        /// <summary> Returns a copy that carries successful persistence evidence. </summary>
        /// <returns> One copied step result with persistence evidence set. </returns>
        internal OperationPhaseStepResult WithPersistence ()
        {
            return this with
            {
                Persisted = true,
            };
        }

        internal static JsonElement? CloneResult (JsonElement? result)
        {
            if (!result.HasValue)
            {
                return null;
            }
            if (result.Value.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException(
                    "An operation result payload must be a JSON object.",
                    nameof(result));
            }

            return result.Value.Clone();
        }
    }
}
