using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one phase-step result for one operation. </summary>
    /// <param name="Applied"> Whether the operation was applied in this step. </param>
    /// <param name="Changed"> Whether this step produced changes. </param>
    /// <param name="Touched"> The touched persistence-unit list produced by this step. </param>
    /// <param name="Failure"> The failure details when this step failed; otherwise <see langword="null" />. </param>
    internal sealed record OperationPhaseStepResult (
        bool Applied,
        bool Changed,
        IReadOnlyList<OperationTouch> Touched,
        OperationFailure? Failure)
    {
        /// <summary> Gets a value indicating whether this step succeeded. </summary>
        public bool IsSuccess => Failure is null;

        /// <summary> Creates a successful phase-step result. </summary>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The successful phase-step result. </returns>
        public static OperationPhaseStepResult Success (
            IReadOnlyList<OperationTouch>? touched = null)
        {
            return Success(
                applied: false,
                changed: false,
                touched: touched);
        }

        /// <summary> Creates a successful phase-step result. </summary>
        /// <param name="applied"> Whether operation was applied in this step. </param>
        /// <param name="changed"> Whether changes were produced in this step. </param>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The successful phase-step result. </returns>
        public static OperationPhaseStepResult Success (
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch>? touched = null)
        {
            return new OperationPhaseStepResult(
                Applied: applied,
                Changed: changed,
                Touched: touched ?? Array.Empty<OperationTouch>(),
                Failure: null);
        }

        /// <summary> Creates a failed phase-step result. </summary>
        /// <param name="failure"> The operation failure details. </param>
        /// <param name="touched"> The touched persistence-unit list. </param>
        /// <returns> The failed phase-step result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static OperationPhaseStepResult Failed (
            OperationFailure failure,
            IReadOnlyList<OperationTouch>? touched = null)
        {
            return Failed(
                failure,
                applied: false,
                changed: false,
                touched: touched);
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
            IReadOnlyList<OperationTouch>? touched = null)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new OperationPhaseStepResult(
                Applied: applied,
                Changed: changed,
                Touched: touched ?? Array.Empty<OperationTouch>(),
                Failure: failure);
        }
    }
}
