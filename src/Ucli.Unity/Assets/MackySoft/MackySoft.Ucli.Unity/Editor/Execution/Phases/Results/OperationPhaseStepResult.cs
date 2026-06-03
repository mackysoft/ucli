using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one phase-step result for one operation. </summary>
    /// <param name="Applied"> Whether the operation was applied in this step. </param>
    /// <param name="Changed"> Whether this step produced changes. </param>
    /// <param name="Touched"> The touched persistence-unit list produced by this step. </param>
    /// <param name="Failure"> The failure details when this step failed; otherwise <see langword="null" />. </param>
    public sealed record OperationPhaseStepResult (
        bool Applied,
        bool Changed,
        IReadOnlyList<OperationTouch> Touched,
        OperationFailure? Failure)
    {
        /// <summary> Gets the optional query result payload produced by this step. </summary>
        public JsonElement? Result { get; init; }

        /// <summary> Gets the optional read-surface invalidations emitted by this step. </summary>
        internal IReadOnlyList<OperationReadInvalidation> ReadInvalidations { get; init; } = Array.Empty<OperationReadInvalidation>();

        /// <summary> Gets non-fatal diagnostics emitted by this step. </summary>
        public IReadOnlyList<OperationDiagnostic> Diagnostics { get; init; } = Array.Empty<OperationDiagnostic>();

        /// <summary> Gets a value indicating whether this step observed successful persistence. </summary>
        internal bool Persisted { get; init; }

        /// <summary> Gets a value indicating whether this step succeeded. </summary>
        public bool IsSuccess => Failure is null;

        /// <summary> Creates a successful phase-step result. </summary>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The successful phase-step result. </returns>
        public static OperationPhaseStepResult Success (
            IReadOnlyList<OperationTouch>? touched = null,
            JsonElement? result = null)
        {
            return Success(
                applied: false,
                changed: false,
                touched: touched,
                result: result);
        }

        /// <summary> Creates a successful phase-step result. </summary>
        /// <param name="applied"> Whether operation was applied in this step. </param>
        /// <param name="changed"> Whether changes were produced in this step. </param>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The successful phase-step result. </returns>
        public static OperationPhaseStepResult Success (
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch>? touched = null,
            JsonElement? result = null)
        {
            return new OperationPhaseStepResult(
                Applied: applied,
                Changed: changed,
                Touched: touched ?? Array.Empty<OperationTouch>(),
                Failure: null)
            {
                Result = CloneResult(result),
            };
        }

        /// <summary> Creates a failed phase-step result. </summary>
        /// <param name="failure"> The operation failure details. </param>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The failed phase-step result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static OperationPhaseStepResult Failed (
            OperationFailure failure,
            IReadOnlyList<OperationTouch>? touched = null,
            JsonElement? result = null)
        {
            return Failed(
                failure,
                applied: false,
                changed: false,
                touched: touched,
                result: result);
        }

        /// <summary> Creates a failed phase-step result. </summary>
        /// <param name="failure"> The operation failure details. </param>
        /// <param name="applied"> Whether operation was applied in this step. </param>
        /// <param name="changed"> Whether changes were produced in this step. </param>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The failed phase-step result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static OperationPhaseStepResult Failed (
            OperationFailure failure,
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch>? touched = null,
            JsonElement? result = null)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new OperationPhaseStepResult(
                Applied: applied,
                Changed: changed,
                Touched: touched ?? Array.Empty<OperationTouch>(),
                Failure: failure)
            {
                Result = CloneResult(result),
            };
        }

        /// <summary> Returns a copy with the supplied read-surface invalidations. </summary>
        /// <param name="readInvalidations"> The invalidations to attach to the step result. </param>
        /// <returns> One copied step result carrying the supplied invalidations. </returns>
        internal OperationPhaseStepResult WithReadInvalidations (IReadOnlyList<OperationReadInvalidation>? readInvalidations)
        {
            return this with
            {
                ReadInvalidations = readInvalidations ?? Array.Empty<OperationReadInvalidation>(),
            };
        }

        /// <summary> Returns a copy with the supplied diagnostics. </summary>
        /// <param name="diagnostics"> The diagnostics to attach to the step result. </param>
        /// <returns> One copied step result carrying the supplied diagnostics. </returns>
        public OperationPhaseStepResult WithDiagnostics (IReadOnlyList<OperationDiagnostic>? diagnostics)
        {
            return this with
            {
                Diagnostics = diagnostics ?? Array.Empty<OperationDiagnostic>(),
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

        private static JsonElement? CloneResult (JsonElement? result)
        {
            if (!result.HasValue)
            {
                return null;
            }

            return result.Value.Clone();
        }
    }
}
